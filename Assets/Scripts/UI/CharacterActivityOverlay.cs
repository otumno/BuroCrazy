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
        if (staff is DirectorAvatarController) identity = "Директор";
        string activity;
        if (mover != null && mover.IsSlipping) activity = $"Упал: {mover.FallReason}";
        else if (TryGetComponent<Clinch.ClinchTarget>(out var clinch) && clinch.IsActive)
            activity = "Затруднение — ждёт помощи";
        else if (client != null) activity = ClientActivity();
        else activity = StaffActivity();
        return identity + "\n" + activity;
    }

    private string ClientActivity()
    {
        var sm = client.stateMachine;
        if (sm == null) return "Появляется";
        string ticket = sm.MyQueueNumber >= 0 ? $" · талон №{sm.MyQueueNumber}" : "";
        var desk = sm.AssignedServicePoint;
        string place = desk != null ? DeskName(desk) : PlaceName(sm.GetCurrentGoal());
        switch (sm.GetCurrentState())
        {
            case ClientState.Spawning: return "Пришёл в учреждение";
            case ClientState.MovingToTerminal: return "Идёт за талоном";
            case ClientState.GettingTicket: return "Получает талон";
            case ClientState.MovingToSeat: return "Идёт к месту ожидания" + ticket;
            case ClientState.AtWaitingArea:
            case ClientState.SittingInWaitingArea: return "Ждёт вызова" + ticket;
            case ClientState.AtLimitedZoneEntrance: return "Ждёт входа: " + place + ticket;
            case ClientState.WaitingForDocument: return "Ждёт документ из архива" + ticket;
            case ClientState.InsideLimitedZone:
            case ClientState.AtRegistration:
            case ClientState.AtDesk1:
            case ClientState.AtDesk2:
            case ClientState.AtCashier:
                if (sm.GetTargetZone() != null && sm.GetTargetZone() == ClientSpawner.GetToiletZone()) return "В туалете";
                var worker = sm.MyServiceProvider as StaffController;
                return worker != null ? $"Обслуживается: {place}\nУ {worker.characterName}" : "Ждёт сотрудника: " + place;
            case ClientState.AtToilet: return "В туалете";
            case ClientState.PassedRegistration: return "Получает направление";
            case ClientState.Confused: return "Растерялся — ищет маршрут";
            case ClientState.Grumbling: return "Ворчит — теряет терпение" + ticket;
            case ClientState.Enraged: return "Буянит";
            case ClientState.Leaving: return "Идёт к выходу";
            case ClientState.LeavingUpset: return "Уходит недовольным";
            default: return "Идёт: " + place + ticket;
        }
    }

    private string StaffActivity()
    {
        if (staff == null) return "Ожидает";
        if (staff is DirectorAvatarController director)
        {
            var desk = director.CurrentWorkstation;
            if (director.ClientBeingServed != null) return $"Обслуживает: {DeskName(desk)}\n{director.ClientBeingServed.name}";
            if (desk != null && desk.CurrentClient != null) return $"Ждёт: {desk.CurrentClient.name}\n{DeskName(desk)}";
            if (director.IsMoving) return "Идёт: " + PlaceName(mover != null ? mover.Destination : null);
            switch (director.GetCurrentState())
            {
                case DirectorAvatarController.DirectorState.AtDesk: return "За столом директора";
                case DirectorAvatarController.DirectorState.WorkingAtStation: return "Работает: " + DeskName(desk);
                case DirectorAvatarController.DirectorState.GoingForDocuments: return "Идёт за документами";
                case DirectorAvatarController.DirectorState.CarryingDocuments: return "Несёт документы";
                default: return "Ожидает команды";
            }
        }
        if (!string.IsNullOrWhiteSpace(staff.CurrentSubStatus)) return staff.CurrentSubStatus;
        var station = (staff as IServiceProvider)?.GetWorkstation() ?? staff.assignedWorkstation;
        string action = staff.currentAction != null ? staff.currentAction.displayName : "";
        if (staff.IsMoving) return $"Идёт: {PlaceName(mover != null ? mover.Destination : null)}" +
            (string.IsNullOrWhiteSpace(action) ? "" : "\nЗадача: " + action);
        if (station != null && station.CurrentClient != null)
            return $"{(station.IsClientPhysicallyReady ? "Обслуживает" : "Ждёт клиента")}: {DeskName(station)}\n{station.CurrentClient.name}";
        if (!string.IsNullOrWhiteSpace(action)) return action;
        switch (staff.GetCurrentStateName())
        {
            case "OffDuty": case "Inactive": return "Не на смене";
            case "OnBreak": return "Отдыхает";
            case "AtToilet": return "В туалете";
            case "GoingToBreak": return "Идёт отдыхать";
            case "GoingToToilet": return "Идёт в туалет";
            case "StressedOut": return "Не справляется со стрессом";
            case "Patrolling": case "ChairPatrol": return "Патрулирует";
            case "Chasing": case "ChasingThief": return "Преследует нарушителя";
            case "WaitingForArchive": return "Ждёт выписку из архива";
            case "Cleaning": return "Убирает";
            case "ReturningToWork": return "Возвращается к работе";
            default: return staff.IsOnDuty() ? "Ожидает задачу" : "Смена закончена";
        }
    }

    public static string DeskName(ServicePoint desk) => desk == null ? "рабочее место" :
        !string.IsNullOrWhiteSpace(desk.friendlyName) ? desk.friendlyName : desk.deskId switch
        {
            0 => "регистратура", -1 => "касса", 1 => "окно №1", 2 => "окно №2", 3 => "архив", _ => "рабочее место"
        };

    private static string PlaceName(Waypoint point)
    {
        if (point == null) return "к назначенной цели";
        if (!string.IsNullOrWhiteSpace(point.friendlyName)) return point.friendlyName;
        var registry = ScenePointsRegistry.Instance;
        if (registry != null)
        {
            foreach (var desk in registry.allServicePoints)
                if (desk != null && (desk.clientStandPoint == point || desk.clerkStandPoint == point.transform || desk.internCollectionPoint == point.transform)) return DeskName(desk);
            if (registry.toiletPoints.Contains(point)) return "туалет";
            if (registry.kitchenPoints.Contains(point)) return "место отдыха";
        }
        string key = point.name.ToLowerInvariant();
        if (key.Contains("toilet")) return "туалет";
        if (key.Contains("exit")) return "выход";
        if (key.Contains("ticket")) return "терминал талонов";
        if (key.Contains("archive")) return "архив";
        return "точка маршрута";
    }

    public static string RoleName(StaffController.Role role) => role switch
    {
        StaffController.Role.Director => "Директор", StaffController.Role.Intern => "Стажёр",
        StaffController.Role.Registrar => "Регистратор", StaffController.Role.Cashier => "Кассир",
        StaffController.Role.Archivist => "Архивариус", StaffController.Role.Guard => "Охранник",
        StaffController.Role.Janitor => "Уборщик", StaffController.Role.ServiceWorker => "Служащий",
        StaffController.Role.Clerk => "Клерк", StaffController.Role.OfficeManager => "Офис-менеджер",
        StaffController.Role.Accountant => "Бухгалтер", _ => "Сотрудник"
    };
}
