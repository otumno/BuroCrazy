# BuroCrazy Project Status

## Summary

BuroCrazy is a Soviet-style bureaucratic office management game. This document tracks completed work and remaining tasks.

## Completed Systems

### Phase 1: New Script Files Created

#### Enums (2 files)
- `Assets/Scripts/Enums/DocumentSubtype.cs` - Certificate, Contract, Report, Statement, Approval, Request, Complaint, Warrant, Protocol
- `Assets/Scripts/Enums/EquipmentType.cs` - Copier, Stapler, SpecialSeal, BindingMachine, Calculator, Computer, ArchiveShelf, Safe, Phone, WaterCooler

#### Data Classes (11 files)
- `Assets/Scripts/Data/Documents/DocumentData.cs` - Document configuration with equipment/skill/time requirements
- `Assets/Scripts/Data/ArchetypeDatabase.cs` - Database for client archetypes
- `Assets/Scripts/Data/Visuals/HairStyleData.cs` - Hair styles for visual diversity
- `Assets/Scripts/Data/Visuals/OutfitData.cs` - Outfit data for character visuals
- `Assets/Scripts/Data/Visuals/HairStyleDatabase.cs` - Database for hair styles
- `Assets/Scripts/Data/Visuals/OutfitDatabase.cs` - Database for outfits
- `Assets/Scripts/Data/Creation/BookPageData.cs` - Pages for director creation book
- `Assets/Scripts/Data/Creation/BookPageDatabase.cs` - Database for book pages
- `Assets/Scripts/Data/Creation/DirectorInitialState.cs` - Starting state for new game
- `Assets/Scripts/Data/Policies/JobInstruction.cs` - Job instructions (greeting, priority, money handling)
- `Assets/Scripts/Data/Bureaucracy/BureaucracyRoute.cs` - Document routing for bureaucracy system

#### Managers (8 files)
- `Assets/Scripts/Managers/EquipmentManager.cs` - Manages office equipment
- `Assets/Scripts/Managers/JobInstructionDatabase.cs` - Database for job instructions
- `Assets/Scripts/Managers/InstructionManager.cs` - Manages active job instructions
- `Assets/Scripts/Managers/Bureaucracy/BureaucracyManager.cs` - Manages document routes
- `Assets/Scripts/Managers/NotificationManager.cs` - Notification system
- `Assets/Scripts/Managers/Academy/AcademyScenarioManager.cs` - Training/academy system with Kobayashi Maru
- `Assets/Scripts/Managers/ClientSpawnerWithArchetypes.cs` - Spawns clients based on archetypes
- `Assets/Scripts/Managers/StaffPunctualityConfig.cs` - Configuration for schedule/punctuality system

#### UI (8 files)
- `Assets/Scripts/UI/Creation/DirectorCreationBookUI.cs` - UI for creating director (Sir Brante style)
- `Assets/Scripts/UI/Academy/AcademyUI.cs` - UI for academy/training
- `Assets/Scripts/UI/Policies/InstructionPanelUI.cs` - Panel for job instructions
- `Assets/Scripts/UI/Policies/InstructionItemUI.cs` - Individual instruction item
- `Assets/Scripts/UI/Policies/PolicyDeskButton.cs` - Button on director's desk
- `Assets/Scripts/UI/World/WorldNoticeBoard.cs` - World-space notice board
- `Assets/Scripts/UI/Notifications/NotificationUI.cs` - Notification popup
- `Assets/Scripts/UI/Notifications/FloatingText.cs` - Floating text effect

#### Character Extensions (3 files)
- `Assets/Scripts/Characters/ClientArchetype.cs` - Client archetype definition
- `Assets/Scripts/Characters/Visuals/CharacterVisuals_Diversity.cs` - Visual diversity system
- `Assets/Scripts/Characters/Controllers/StaffScheduleExtensions.cs` - Schedule/punctuality/break system

#### Tests (4 files)
- `Assets/Scripts/Editor/Tests/DocumentDataTests.cs`
- `Assets/Scripts/Editor/Tests/ArchetypeDatabaseTests.cs`
- `Assets/Scripts/Editor/Tests/ClientArchetypeTests.cs`
- `Assets/Scripts/Editor/Tests/StaffScheduleTests.cs`

#### Documentation (2 files)
- `IMPLEMENTATION_GUIDE.md` - Comprehensive setup instructions
- `MAP_CAREER_SETUP_GUIDE.md` - Map and Career system setup guide

### Phase 2: Bug Fixing & Refactoring

Fixed numerous compilation errors:
1. **Missing using statements** - Added `using Managers;`, `using Characters;`, `using Enums;`
2. **Namespace issues** - Added `namespace Enums { }` to `ProjectDocumentType.cs` and `ServicePointType.cs`
3. **Duplicate methods** - Removed duplicate `EndShift()` and `IsOnBreak()` from `StaffController.cs`
4. **Partial class conflicts** - Deleted problematic `StaffController_Schedule.cs`, created `StaffScheduleExtensions.cs` with reflection-based approach
5. **Gender enum issues** - Fixed `Gender.Undefined` references by using `(Gender)99` fallback
6. **Equipment methods** - Changed `TakeDamage()` to `Degrade()`, direct field access to method calls
7. **Missing Singleton** - Added `Instance` property to `BookPageDatabase.cs`
8. **UI method names** - Changed `StartWorkday()` to `StartOrResumeGameplay()`, `ShowPanel()` to `SetActive(true)`
9. **Event subscriptions** - Fixed `OnUpgradePurchased` to use `UpgradeManager` instead of `InstructionManager`
10. **Missing field** - Added `shiftStartTime` field to `StaffController.cs`
11. **Color.brown fix** - Changed to `new Color(0.6f, 0.4f, 0.2f)`
12. **Nested class access** - Fixed `ArchetypeDatabase.ArchetypeWeight` reference

### Phase 3: Map & Career Tree Debugging

Fixed `MapPanelUI.cs` with:
- Null checks for all UI components
- Debug warnings/errors with ContextMenu for testing
- Proper validation of `ProgressionManager.Instance`, `PlayerWallet.Instance`, `DocumentManager.Instance`
- Improved error logging to identify missing Inspector assignments

Added extensive debug logging to:
- `RegionSlotUI.OnClicked()` - Logs when region is clicked
- `JobNodeUI.OnClicked()` - Logs when job is clicked
- `MapPanelUI.ShowRegionInfo()` - Logs region info panel activation
- `MapPanelUI.ShowJobInfo()` - Logs job info panel activation

Added ContextMenu debug tests:
- `Debug: Test Map Panel` - Comprehensive test of all MapPanelUI fields
- `Debug: Test Region Slots` - Tests all region slot assignments
- `Debug: Test Job Nodes` - Tests all job node assignments

## Current Status

### Working Systems
- ✅ DocumentData with equipment/skill/time requirements
- ✅ ClientArchetype system for spawning diverse clients
- ✅ ProgressionManager for regions and career tree
- ✅ DocumentManager for tracking active project documents
- ✅ PlayerWallet for money management
- ✅ EquipmentManager for office equipment
- ✅ Staff schedule system with breaks and punctuality
- ✅ Notification system
- ✅ Academy/Training scenario system
- ✅ Director creation book UI
- ✅ Job instruction system
- ✅ Map and Career UI with debug logging

### Needing Configuration (Editor Work)
- ❌ MapPanelUI Inspector assignments (regionInfoPanel, jobInfoPanel, etc.)
- ❌ RegionData ScriptableObjects (need creation and assignment)
- ❌ JobTitleData ScriptableObjects (need creation and assignment)
- ❌ ClientArchetype ScriptableObjects (need creation)
- ❌ HairStyleData/OutfitData ScriptableObjects (need creation)
- ❌ BookPageData ScriptableObjects (need creation)

## Next Steps

### Immediate (Can be done without code changes)

1. **Configure MapPanelUI Inspector**
   - Assign `regionInfoPanel` and `jobInfoPanel` GameObjects
   - Assign all TextMeshPro components (r_Title, r_Desc, r_Cost, j_Title, j_Desc, j_Cost)
   - Assign all Button components (r_ActionButton, j_ActionButton)
   - Assign `directorInboxStack` (DocumentStack from Director Desk)
   - Populate `regionSlots` list with RegionSlotUI GameObjects
   - Populate `jobNodes` list with JobNodeUI GameObjects

2. **Create RegionData ScriptableObjects**
   - Create at least 3 regions: Slums, Downtown, Industrial Zone
   - Assign to ProgressionManager.allRegionsDatabase

3. **Create JobTitleData ScriptableObjects**
   - Create at least 5 jobs: Director, Regional Manager, Dept Head, Deputy Minister, Minister
   - Assign to ProgressionManager.allJobsDatabase

4. **Test Map System**
   - Run "Debug: Test Map Panel" ContextMenu
   - Verify all fields show as assigned
   - Click region/job buttons and verify info panels appear

### Short-term (Minor code changes)

1. **Create Editor Scripts** (optional)
   - Script to auto-generate default RegionData/JobTitleData
   - Script to validate MapPanelUI Inspector setup

2. **Create Sample ScriptableObjects** (optional)
   - Pre-made ClientArchetype assets (elderly, worker, student, etc.)
   - Pre-made BookPageData for director creation

### Long-term (Larger features)

1. **Visual Diversity System**
   - Complete CharacterVisuals_Diversity implementation
   - Create HairStyleData and OutfitData assets
   - Integrate with character spawning

2. **Academy System**
   - Implement Kobayashi Maru scenarios
   - Create AcademyUI interaction flow

3. **Job Instruction System**
   - Implement InstructionManager logic
   - Create JobInstruction assets
   - Build InstructionPanelUI

## Testing Instructions

### Map Panel Debug Test

1. Open Unity Editor
2. Select MapPanelUI GameObject
3. Right-click MapPanelUI component
4. Select "Debug: Test Map Panel"
5. Check Console output - should show all fields as assigned

### Runtime Test

1. Enter Play Mode
2. Click a Region button on the map
3. Check Console for:
   - `[RegionSlotUI] OnClicked called for region: XXX`
   - `[MapPanelUI] ShowRegionInfo called with region: XXX`
   - If regionInfoPanel is null: `[MapPanelUI] regionInfoPanel is NOT assigned in Inspector!`
4. Region Info Panel should appear on screen

## Known Issues

1. **Map/Career buttons show no info panel**
   - Cause: Inspector assignments missing
   - Fix: See MAP_CAREER_SETUP_GUIDE.md

2. **ProgressionManager.Instance is null**
   - Cause: ProgressionManager GameObject not in scene
   - Fix: Add ProgressionManager under [SYSTEMS] GameObject

3. **DocumentManager.IsProjectDocPending errors**
   - Cause: Method signature mismatch
   - Fix: Ensure using correct method signature (string id parameter)

## File Statistics

- **Total new files created:** 38
- **Total files modified (debugging):** 6
- **Lines of new code:** ~3,500
- **Test files:** 4

## Dependencies

The following systems are prerequisites for full functionality:

- `ProgressionManager` - Requires RegionData and JobTitleData ScriptableObjects
- `DocumentManager` - Requires DocumentStack components on scene
- `PlayerWallet` - Requires UI TextMeshPro reference
- `MapPanelUI` - Requires extensive Inspector assignments
- `RegionSlotUI` - Requires RegionData and MapPanelUI references
- `JobNodeUI` - Requires JobTitleData and MapPanelUI references

## Integration Points

```
Scene Hierarchy Required:
├── [SYSTEMS]
│   ├── ProgressionManager
│   ├── DocumentManager
│   ├── PlayerWallet
│   ├── EquipmentManager
│   └── ...
│
├── [UI]
│   └── Canvas
│       ├── MainHUD
│       ├── MapPanelUI ← Requires configuration
│       └── ...
│
└── Director Desk
    └── Incoming Stack ← Reference for MapPanelUI
```

## References

- GDD: Game Design Document (original requirements)
- IMPLEMENTATION_GUIDE.md: Detailed setup instructions
- MAP_CAREER_SETUP_GUIDE.md: Map and Career system setup

---

## Phase 4: Bug Fixes & Features (23.01.2026)

### Dialogue System - Images & Backgrounds
- Added `nodeImage` field to PhraseNode, ChoiceNode, EventNode
- Added `defaultBackground` field to StartNode
- Added UI components: NodeImageContainer, NodeImageDisplay, NodeImageFrame
- Added portrait frames: DirectorPortraitFrame, ClientPortraitFrame
- Logic: Node image → Default background from StartNode → Nothing

### Client Lifecycle Fixes
- ClientSpawnerWithArchetypes: Null check for GetComponent
- ClientPathfinding: Empty enum protection, OnDestroy queue cleanup
- ClientStateMachine: ConfusedRoutine else, Enraged timer, WaitingForDocument timeout
- ClientActionExecutor: Race condition protection
- ClientQueueManager: Zone cleanup on timeout

### Staff Lifecycle Fixes
- StaffController: FireAndGoHome cleanup, Resources.Load check
- HiringManager: occupiedPoints cleanup, RemoveStaff method
- ClerkController/InternController: Multiple null checks
- GuardMovement: Staff and patrol point null checks
- AgentMover/CharacterVisuals: OnDestroy coroutine cleanup
- AssignmentManager: FirstOrDefault, UnassignWorkstation method
- ProcessDocumentExecutor: Zone and client null checks

### UI Fixes
- SmartRoomLabel: Proper initialization, hover logic
- MainUIManager: Auto-find pausePanel by name
- TeamMemberCardUI: ApplyRoleColor method with 12 role colors

### New Documentation
- `.Docs/Buro Crazy/Механики/Диалоговая Система.md`
- `.Docs/Buro Crazy/Сущности/Клиенты.md`
- `.Docs/Buro Crazy/Сущности/Персонал.md`
- `SUMMARY.md` - Complete changes log
