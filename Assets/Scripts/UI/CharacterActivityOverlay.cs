using TMPro;
using UnityEngine;
using Managers;

// Separate from pause-only needs panels and transient thought bubbles: activity is always visible.
[DisallowMultipleComponent]
public sealed class CharacterActivityOverlay : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, .65f, 0f);
    private Transform headAnchor;
    private RectTransform root;
    private TextMeshProUGUI label;
    private StaffController staff;
    private ClientPathfinding client;
    private AgentMover mover;
    private float nextRefresh;
    private float worldScale = .01f;
    public string DisplayedText => label != null ? label.text : "";

    public static void Ensure(GameObject actor)
    {
        if (actor.GetComponent<CharacterActivityOverlay>() == null)
            actor.AddComponent<CharacterActivityOverlay>();
    }

    private void Awake()
    {
        staff = GetComponent<StaffController>();
        client = GetComponent<ClientPathfinding>();
        mover = GetComponent<AgentMover>();
        headAnchor = GetComponent<StaffPrefabReferences>()?.headAttachPoint;
        var style = Resources.Load<CharacterActivityStyle>("UI/CharacterActivityStyle");
        if (style != null) { offset = style.offset; worldScale = style.worldScale; }
        // Do not depend on a particular character prefab: hires, promotions and saved clients share this UI.
        var panel = new GameObject("ActivityStatus", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.Image));
        root = panel.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.sizeDelta = style != null ? style.panelSize : new Vector2(250f, 82f);
        root.pivot = new Vector2(.5f, 0f);
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31990;
        var background = panel.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(.035f, .045f, .06f, .84f);
        background.raycastTarget = false;
        var textObject = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root, false);
        label = textObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(7f, 4f);
        label.rectTransform.offsetMax = new Vector2(-7f, -4f);
        label.font = style != null && style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        label.fontSize = 18f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
        label.raycastTarget = false;
        label.color = Color.white;
    }

    private void LateUpdate()
    {
        if (root == null) return;
        // The body can flip or rotate during a fall; the status must remain upright and readable.
        root.position = (headAnchor != null ? headAnchor.position : transform.position + Vector3.up * .7f) + offset;
        root.rotation = Quaternion.identity;
        var scale = transform.lossyScale;
        root.localScale = new Vector3(ScaleFactor(scale.x), ScaleFactor(scale.y), ScaleFactor(scale.z));
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .2f;
        if (staff == null) staff = GetComponent<StaffController>(); // A promotion replaces the role controller.
        if (client == null) client = GetComponent<ClientPathfinding>();
        bool visible = (staff != null || client != null) && !(client != null && client.IsRemote);
        root.gameObject.SetActive(visible);
        if (!visible) return;
        label.text = BuildText();
        label.color = mover != null && mover.IsSlipping ? new Color(1f, .72f, .4f) : Color.white;
    }

    private float ScaleFactor(float parentScale) => worldScale / (Mathf.Abs(parentScale) > .001f ? parentScale : 1f);

    public string BuildText()
    {
        string identity = staff != null ? RoleName(staff.currentRole) : name;
        if (staff != null && !string.IsNullOrWhiteSpace(staff.characterName)) identity += " · " + staff.characterName;
        if (staff is DirectorAvatarController) identity = UI.GameText.Get("role.director");
        string activity;
        if (mover != null && mover.IsSlipping) activity = UI.GameText.Get("activity.falling", mover.FallReason);
        else if (TryGetComponent<Clinch.ClinchTarget>(out var clinch) && clinch.IsActive)
            activity = UI.GameText.Get("activity.clinch");
        else if (client != null) activity = ClientActivity();
        else activity = StaffActivity();
        return identity + "\n" + activity;
    }

    private string ClientActivity()
    {
        var sm = client.stateMachine;
        if (sm == null) return UI.GameText.Get("activity.spawning");
        string ticket = sm.MyQueueNumber >= 0 ? UI.GameText.Get("activity.ticket", sm.MyQueueNumber) : "";
        var desk = sm.AssignedServicePoint;
        string place = desk != null ? DeskName(desk) : PlaceName(sm.GetCurrentGoal());
        switch (sm.GetCurrentState())
        {
            case ClientState.Spawning: return UI.GameText.Get("activity.arrived");
            case ClientState.MovingToTerminal: return UI.GameText.Get("activity.going_ticket");
            case ClientState.GettingTicket: return UI.GameText.Get("activity.getting_ticket");
            case ClientState.MovingToSeat: return UI.GameText.Get("activity.going_wait") + ticket;
            case ClientState.AtWaitingArea:
            case ClientState.SittingInWaitingArea: return UI.GameText.Get("activity.waiting_call") + ticket;
            case ClientState.AtLimitedZoneEntrance: return UI.GameText.Get("activity.waiting_entry") + place + ticket;
            case ClientState.WaitingForDocument: return UI.GameText.Get("activity.waiting_document") + ticket;
            case ClientState.InsideLimitedZone:
            case ClientState.AtRegistration:
            case ClientState.AtDesk1:
            case ClientState.AtDesk2:
            case ClientState.AtCashier:
                if (sm.GetTargetZone() != null && sm.GetTargetZone() == ClientSpawner.GetToiletZone()) return UI.GameText.Get("staff.toilet.using");
                var worker = sm.MyServiceProvider as StaffController;
                return worker != null ? UI.GameText.Get("activity.being_served", place, worker.characterName) : UI.GameText.Get("activity.waiting_staff") + place;
            case ClientState.AtToilet: return UI.GameText.Get("staff.toilet.using");
            case ClientState.PassedRegistration: return UI.GameText.Get("activity.direction");
            case ClientState.Confused: return UI.GameText.Get("activity.confused");
            case ClientState.Grumbling: return UI.GameText.Get("activity.grumbling") + ticket;
            case ClientState.Enraged: return UI.GameText.Get("activity.enraged");
            case ClientState.Leaving: return UI.GameText.Get("activity.exiting");
            case ClientState.LeavingUpset: return UI.GameText.Get("activity.leaving_upset");
            default: return UI.GameText.Get("activity.moving_prefix") + place + ticket;
        }
    }

    private string StaffActivity()
    {
        if (staff == null) return UI.GameText.Get("activity.waiting");
        if (staff is DirectorAvatarController director)
        {
            var desk = director.CurrentWorkstation;
            if (director.ClientBeingServed != null) return UI.GameText.Get("staff.service.serving", DeskName(desk), director.ClientBeingServed.name);
            if (desk != null && desk.CurrentClient != null) return UI.GameText.Get("activity.waiting_client", desk.CurrentClient.name, DeskName(desk));
            if (director.IsMoving) return UI.GameText.Get("activity.moving_prefix") + PlaceName(mover != null ? mover.Destination : null);
            switch (director.GetCurrentState())
            {
                case DirectorAvatarController.DirectorState.AtDesk: return UI.GameText.Get("activity.director_desk");
                case DirectorAvatarController.DirectorState.WorkingAtStation: return UI.GameText.Get("activity.working") + DeskName(desk);
                case DirectorAvatarController.DirectorState.GoingForDocuments: return UI.GameText.Get("activity.collecting_documents");
                case DirectorAvatarController.DirectorState.CarryingDocuments: return UI.GameText.Get("activity.carrying_documents");
                default: return UI.GameText.Get("activity.awaiting_command");
            }
        }
        if (!string.IsNullOrWhiteSpace(staff.CurrentSubStatus)) return staff.CurrentSubStatus;
        var station = (staff as IServiceProvider)?.GetWorkstation() ?? staff.assignedWorkstation;
        string action = staff.currentAction != null ? staff.currentAction.LocalizedDisplayName : "";
        if (staff.IsMoving) return UI.GameText.Get("activity.moving", PlaceName(mover != null ? mover.Destination : null)) +
            (string.IsNullOrWhiteSpace(action) ? "" : UI.GameText.Get("activity.task", action));
        if (station != null && station.CurrentClient != null)
            return UI.GameText.Get(station.IsClientPhysicallyReady ? "staff.service.serving" : "activity.waiting_at_desk", DeskName(station), station.CurrentClient.name);
        if (!string.IsNullOrWhiteSpace(action)) return action;
        switch (staff.GetCurrentStateName())
        {
            case "OffDuty": case "Inactive": return UI.GameText.Get("activity.off_duty");
            case "OnBreak": return UI.GameText.Get("staff.break.resting");
            case "AtToilet": return UI.GameText.Get("staff.toilet.using");
            case "GoingToBreak": return UI.GameText.Get("activity.going_break");
            case "GoingToToilet": return UI.GameText.Get("activity.going_toilet");
            case "StressedOut": return UI.GameText.Get("activity.stressed");
            case "Patrolling": case "ChairPatrol": return UI.GameText.Get("activity.patrolling");
            case "Chasing": case "ChasingThief": return UI.GameText.Get("activity.chasing");
            case "WaitingForArchive": return UI.GameText.Get("activity.waiting_record");
            case "Cleaning": return UI.GameText.Get("activity.cleaning");
            case "ReturningToWork": return UI.GameText.Get("activity.returning");
            default: return staff.IsOnDuty() ? UI.GameText.Get("activity.waiting_task") : UI.GameText.Get("activity.shift_finished");
        }
    }

    public static string DeskName(ServicePoint desk)
    {
        if (desk == null) return UI.GameText.Get("place.workstation");
        if (!string.IsNullOrWhiteSpace(desk.friendlyName))
            return UI.GameText.GetAssetText("place.desk." + desk.name, desk.friendlyName);
        return UI.GameText.Get(desk.deskId switch
        {
            0 => "place.registration", -1 => "place.cashier", 1 => "place.desk1", 2 => "place.desk2",
            3 => "place.archive", 4 => "place.bookkeeping", _ => "place.workstation"
        });
    }

    public static string PlaceName(Waypoint point)
    {
        if (point == null) return UI.GameText.Get("place.destination");
        var registry = ScenePointsRegistry.Instance;
        if (registry != null)
        {
            foreach (var desk in registry.allServicePoints)
                if (desk != null && (desk.clientStandPoint == point || desk.clerkStandPoint == point.transform || desk.internCollectionPoint == point.transform)) return DeskName(desk);
            if (registry.toiletPoints.Contains(point)) return UI.GameText.Get("place.toilet");
            if (registry.kitchenPoints.Contains(point)) return UI.GameText.Get("place.break");
        }
        if (!string.IsNullOrWhiteSpace(point.friendlyName))
            return UI.GameText.GetAssetText("place.label." + point.friendlyName, point.friendlyName);
        string key = point.name.ToLowerInvariant();
        if (key.Contains("toilet")) return UI.GameText.Get("place.toilet");
        if (key.Contains("exit")) return UI.GameText.Get("place.exit");
        if (key.Contains("ticket")) return UI.GameText.Get("place.ticket_terminal");
        if (key.Contains("archive")) return UI.GameText.Get("place.archive");
        return UI.GameText.Get("place.waypoint");
    }

    public static string RoleName(StaffController.Role role) => role == StaffController.Role.Unassigned
        ? UI.GameText.Get("role.staff") : UI.StaffRoleNames.GetDisplayName(role);
}
