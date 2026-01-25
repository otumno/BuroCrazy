# Map & Career System Setup Guide

This guide explains how to set up the Map Panel and Career Tree systems in Unity Editor.

## Quick Diagnosis

If clicking Map/Career buttons shows no info panel:

1. Open Unity Editor
2. Select the MapPanelUI GameObject
3. Right-click on MapPanelUI component → "Debug: Test Map Panel"
4. Check Console for what is `null`

## Required Scene Setup

### 1. Managers (Must exist on scene)

Ensure these GameObjects exist in your scene hierarchy:

```
[SYSTEMS] (from SystemsBootstrapper)
├── ProgressionManager
├── DocumentManager
├── PlayerWallet
├── MainUIManager
└── ...other managers

[UI]
├── Canvas
│   ├── MainHUD
│   ├── MapPanelUI      ← Select this GameObject
│   └── ...other UI
```

**ProgressionManager Inspector needs:**
- `All Regions Database` - List of RegionData ScriptableObjects
- `All Jobs Database` - List of JobTitleData ScriptableObjects

**PlayerWallet Inspector needs:**
- `Money Text` - Reference to TMP_Text component

**DocumentManager** - Auto-discovers DocumentStacks at runtime

### 2. MapPanelUI Inspector Assignments

Select the MapPanelUI GameObject and ensure these fields are assigned:

```
MapPanelUI Component:
├── Close Button → UI Button (X button)
├── Region Info Panel → GameObject containing region details
│   ├── R: Title → TextMeshPro component
│   ├── R: Desc → TextMeshPro component  
│   ├── R: Cost → TextMeshPro component
│   ├── R: Action Button → Button component
│   ├── R: Button Text → TextMeshPro component
│   └── Region Doc Prefab → Red folder prefab
│
├── Job Info Panel → GameObject containing job details
│   ├── J: Title → TextMeshPro component
│   ├── J: Desc → TextMeshPro component
│   ├── J: Cost → TextMeshPro component
│   ├── J: Action Button → Button component
│   └── J: Button Text → TextMeshPro component
│
├── Director Inbox Stack → DocumentStack from Director Desk
│
├── Region Slots → List of RegionSlotUI components
│   └── Assign all RegionSlotUI GameObjects from scene
│
└── Job Nodes → List of JobNodeUI components
    └── Assign all JobNodeUI GameObjects from scene
```

### 3. RegionSlotUI Inspector Assignments

Each RegionSlotUI GameObject needs:

```
RegionSlotUI Component:
├── Region Image → Image component (shows region sprite)
├── Lock Icon → Image component (shows padlock when locked)
├── Select Button → Button component
├── Locked Color → Gray color
└── Unlocked Color → White color
```

### 4. JobNodeUI Inspector Assignments

Each JobNodeUI GameObject needs:

```
JobNodeUI Component:
├── Title Text → TextMeshPro component
├── BG Image → Image component
├── Select Button → Button component
├── Locked Overlay → GameObject (shown when locked)
├── Owned Color → Green
├── Available Color → Yellow
└── Locked Color → Gray
```

## Creating ScriptableObjects

### RegionData (for Map)

1. Right-click in Project window
2. Select: Create → Bureau → Progression → Region Data
3. Create at least 3 regions:

**Region 1: Slums (Starting Region)**
```
Region ID: SLUMS
Display Name: Трущобы
Description: Бедный район с надоедливыми клиентами
Unlock Cost Influence: 0
Unlock Cost Money: 0
Map Visual: Assign a sprite
```

**Region 2: Downtown**
```
Region ID: DOWNTOWN
Display Name: Центр
Description: Деловой район с важными клиентами
Unlock Cost Influence: 200
Unlock Cost Money: 1000
Map Visual: Assign a sprite
```

**Region 3: Industrial Zone**
```
Region ID: INDUSTRIAL
Display Name: Промзона
Description: Заводской район
Unlock Cost Influence: 500
Unlock Cost Money: 2500
Map Visual: Assign a sprite
```

**Period Bonuses (optional):**
```
Add PeriodBonus entries for spawn bonuses
- Morning: +2 clients
- Afternoon: +1 client
- Evening: +3 clients
```

### JobTitleData (for Career Tree)

1. Right-click in Project window
2. Select: Create → Bureau → Progression → Job Title Data
3. Create at least 5 jobs:

**Job 1: Director (Starting)**
```
Job ID: DIRECTOR
Title Name: Директор
Description: Начальник учреждения
Tier Level: 0
Required Previous Job: ← Leave empty
Cost Influence: 0
Cost Money: 0
Required Captured Regions: 0
Is Minister Position: ← Unchecked
```

**Job 2: Regional Manager**
```
Job ID: REGIONAL_MANAGER
Title Name: Региональный Менеджер
Description: Управляющий несколькими районами
Tier Level: 1
Required Previous Job: ← Assign Director
Cost Influence: 150
Cost Money: 500
Required Captured Regions: 2
Is Minister Position: ← Unchecked
```

**Job 3: Department Head**
```
Job ID: DEPT_HEAD
Title Name: Начальник Отдела
Description: Руководитель департамента
Tier Level: 2
Required Previous Job: ← Assign Regional Manager
Cost Influence: 300
Cost Money: 1000
Required Captured Regions: 4
Is Minister Position: ← Unchecked
```

**Job 4: Deputy Minister**
```
Job ID: DEPUTY_MINISTER
Title Name: Заместитель Министра
Description: Правая рука министра
Tier Level: 3
Required Previous Job: ← Assign Department Head
Cost Influence: 600
Cost Money: 2500
Required Captured Regions: 6
Is Minister Position: ← Unchecked
```

**Job 5: Minister (Victory)**
```
Job ID: MINISTER
Title Name: Министр
Description: Глава всего ведомства
Tier Level: 4
Required Previous Job: ← Assign Deputy Minister
Cost Influence: 1000
Cost Money: 5000
Required Captured Regions: 8
Is Minister Position: ← CHECKED (Victory condition!)
```

## Assign ScriptableObjects to ProgressionManager

1. Select ProgressionManager GameObject
2. In Inspector, find `All Regions Database`
3. Click "+" and drag your RegionData assets
4. Find `All Jobs Database`
5. Click "+" and drag your JobTitleData assets

## Scene Hierarchy Example

```
Hierarchy:
├── [SYSTEMS] (SystemsBootstrapper)
│   ├── ProgressionManager ← Assign RegionData and JobTitleData here
│   ├── DocumentManager
│   ├── PlayerWallet ← Assign moneyText TMP component
│   └── ...
│
├── Director Desk
│   └── Incoming Stack ← Assign this to MapPanelUI.directorInboxStack
│
└── [UI]
    └── Canvas
        └── MapPanelUI ← Select and configure all Inspector fields
            ├── Info Panels (regionInfoPanel, jobInfoPanel)
            ├── Buttons and Text components
            ├── Region Slots list (drag RegionSlotUI GameObjects)
            └── Job Nodes list (drag JobNodeUI GameObjects)
```

## Testing Checklist

After setup, test in this order:

1. [ ] Open Unity Console
2. [ ] Right-click MapPanelUI → "Debug: Test Map Panel"
   - [ ] All fields should show `assigned: True`
   - [ ] ProgressionManager.Instance, PlayerWallet.Instance, DocumentManager.Instance should show as not null
3. [ ] Enter Play Mode
4. [ ] Click a Region button
   - [ ] Console should log: `[RegionSlotUI] OnClicked called for region: XXX`
   - [ ] Console should log: `[MapPanelUI] ShowRegionInfo called with region: XXX`
   - [ ] Region Info Panel should appear on screen
5. [ ] Click a Job button
   - [ ] Console should log: `[JobNodeUI] OnClicked called for job: XXX`
   - [ ] Console should log: `[MapPanelUI] ShowJobInfo called with job: XXX`
   - [ ] Job Info Panel should appear on screen

## Common Issues

### "regionInfoPanel is NOT assigned in Inspector!"
**Fix:** Drag the region info panel GameObject to the `Region Info Panel` field in MapPanelUI Inspector.

### "ProgressionManager.Instance is null!"
**Fix:** Ensure ProgressionManager GameObject exists in scene hierarchy under [SYSTEMS].

### "PlayerWallet.Instance is null!"
**Fix:** Ensure PlayerWallet GameObject exists in scene hierarchy.

### Buttons appear but nothing happens
**Fix:** Check that `regionSlots` and `jobNodes` lists in MapPanelUI have all the slot/node GameObjects assigned.

### No Region/Job data showing
**Fix:** Ensure RegionData and JobTitleData ScriptableObjects are created and assigned to ProgressionManager.

## Debug Commands Available

In Unity Editor, right-click on MapPanelUI component:

- **Debug: Test Map Panel** - Shows all assigned/null fields
- **Debug: Test Region Slots** - Lists all region slots and their data
- **Debug: Test Job Nodes** - Lists all job nodes and their data
