# 临时生成器：生成 Pause / Death / Victory 三个 UIForm 预制体。
# 不提交（放在工程根目录、跑完即删），只负责产出与 ToastForm/MainMenuForm 同风格的 YAML。
$ErrorActionPreference = 'Stop'

$scriptIds = @{
    Pause   = '85623694e1bfd7b2d25779033403f197'
    Death   = '0739bb53fda1197019d2c967e56185ec'
    Victory = 'b830f5064d67a92a35f83dc19e14d4bb'
}

function New-Lines { $script:__buf = @() }
function Add-Lines { param([string[]]$l) $script:__buf += $l }

function Add-GameObject {
    param([long]$id, [string]$name, [long[]]$components)
    Add-Lines @(
        "--- !u!1 &$id",
        'GameObject:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        '  serializedVersion: 6',
        '  m_Component:'
    )
    foreach ($c in $components) { Add-Lines @("  - component: {fileID: $c}") }
    Add-Lines @(
        '  m_Layer: 5',
        "  m_Name: $name",
        '  m_TagString: Untagged',
        '  m_Icon: {fileID: 0}',
        '  m_NavMeshLayer: 0',
        '  m_StaticEditorFlags: 0',
        '  m_IsActive: 1'
    )
}

function Vec2 { param($x, $y) return "{x: $x, y: $y}" }

function Add-RectTransform {
    param([long]$id, [long]$goId, [long]$fatherId, [long[]]$children,
          [string]$anchorMin = '{x: 0.5, y: 0.5}', [string]$anchorMax = '{x: 0.5, y: 0.5}',
          [string]$sizeDelta = '{x: 100, y: 100}', [string]$anchoredPosition = '{x: 0, y: 0}')
    Add-Lines @(
        "--- !u!224 &$id",
        'RectTransform:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}',
        '  m_LocalPosition: {x: 0, y: 0, z: 0}',
        '  m_LocalScale: {x: 1, y: 1, z: 1}',
        '  m_ConstrainProportionsScale: 0'
    )
    if ($children.Count -eq 0) {
        Add-Lines @('  m_Children: []')
    }
    else {
        Add-Lines @('  m_Children:')
        foreach ($c in $children) { Add-Lines @("  - {fileID: $c}") }
    }
    Add-Lines @(
        "  m_Father: {fileID: $fatherId}",
        '  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}',
        "  m_AnchorMin: $anchorMin",
        "  m_AnchorMax: $anchorMax",
        "  m_AnchoredPosition: $anchoredPosition",
        "  m_SizeDelta: $sizeDelta",
        '  m_Pivot: {x: 0.5, y: 0.5}'
    )
}

function Add-CanvasRenderer {
    param([long]$id, [long]$goId)
    Add-Lines @(
        "--- !u!222 &$id",
        'CanvasRenderer:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_CullTransparentMesh: 1'
    )
}

function Add-Image {
    param([long]$id, [long]$goId, [string]$color, [int]$raycast = 1)
    Add-Lines @(
        "--- !u!114 &$id",
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        '  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}',
        '  m_Name: ',
        '  m_EditorClassIdentifier: ',
        '  m_Material: {fileID: 0}',
        "  m_Color: $color",
        "  m_RaycastTarget: $raycast",
        '  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}',
        '  m_Maskable: 1',
        '  m_OnCullStateChanged:',
        '    m_PersistentCalls:',
        '      m_Calls: []',
        '  m_Sprite: {fileID: 0}',
        '  m_Type: 0',
        '  m_PreserveAspect: 0',
        '  m_FillCenter: 1',
        '  m_FillMethod: 4',
        '  m_FillAmount: 1',
        '  m_FillClockwise: 1',
        '  m_FillOrigin: 0',
        '  m_UseSpriteMesh: 0',
        '  m_PixelsPerUnitMultiplier: 1'
    )
}

function Add-CanvasGroup {
    param([long]$id, [long]$goId)
    Add-Lines @(
        "--- !u!225 &$id",
        'CanvasGroup:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_Enabled: 1',
        '  m_Alpha: 1',
        '  m_Interactable: 1',
        '  m_BlocksRaycasts: 1',
        '  m_IgnoreParentGroups: 0'
    )
}

function Add-Text {
    param([long]$id, [long]$goId, [string]$text, [int]$fontSize = 36,
          [int]$alignment = 4, [string]$color = '{r: 1, g: 1, b: 1, a: 1}')
    Add-Lines @(
        "--- !u!114 &$id",
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        '  m_Script: {fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}',
        '  m_Name: ',
        '  m_EditorClassIdentifier: ',
        '  m_Material: {fileID: 0}',
        "  m_Color: $color",
        '  m_RaycastTarget: 0',
        '  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}',
        '  m_Maskable: 1',
        '  m_OnCullStateChanged:',
        '    m_PersistentCalls:',
        '      m_Calls: []',
        '  m_FontData:',
        '    m_Font: {fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}',
        "    m_FontSize: $fontSize",
        '    m_FontStyle: 0',
        '    m_BestFit: 0',
        '    m_MinSize: 10',
        '    m_MaxSize: 120',
        "    m_Alignment: $alignment",
        '    m_AlignByGeometry: 0',
        '    m_RichText: 1',
        '    m_HorizontalOverflow: 1',
        '    m_VerticalOverflow: 1',
        '    m_LineSpacing: 1',
        "  m_Text: $text"
    )
}

function Add-Button {
    param([long]$id, [long]$goId, [long]$targetGraphicId)
    Add-Lines @(
        "--- !u!114 &$id",
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        '  m_Script: {fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff, type: 3}',
        '  m_Name: ',
        '  m_EditorClassIdentifier: ',
        '  m_Navigation:',
        '    m_Mode: 3',
        '    m_WrapAround: 0',
        '    m_SelectOnUp: {fileID: 0}',
        '    m_SelectOnDown: {fileID: 0}',
        '    m_SelectOnLeft: {fileID: 0}',
        '    m_SelectOnRight: {fileID: 0}',
        '  m_Transition: 1',
        '  m_Colors:',
        '    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}',
        '    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}',
        '    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}',
        '    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}',
        '    m_DisabledColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}',
        '    m_ColorMultiplier: 1',
        '    m_FadeDuration: 0.1',
        '  m_SpriteState:',
        '    m_HighlightedSprite: {fileID: 0}',
        '    m_PressedSprite: {fileID: 0}',
        '    m_SelectedSprite: {fileID: 0}',
        '    m_DisabledSprite: {fileID: 0}',
        '  m_AnimationTriggers:',
        '    m_NormalTrigger: Normal',
        '    m_HighlightedTrigger: Highlighted',
        '    m_PressedTrigger: Pressed',
        '    m_SelectedTrigger: Selected',
        '    m_DisabledTrigger: Disabled',
        '  m_Interactable: 1',
        "  m_TargetGraphic: {fileID: $targetGraphicId}",
        '  m_OnClick:',
        '    m_PersistentCalls:',
        '      m_Calls: []'
    )
}

function Add-FormScript {
    param([long]$id, [long]$goId, [string]$scriptGuid, [string[]]$fields)
    Add-Lines @(
        "--- !u!114 &$id",
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        "  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}",
        '  m_Name: ',
        '  m_EditorClassIdentifier: '
    )
    foreach ($f in $fields) { Add-Lines @("  $f") }
}

# ---------------------------------------------------------------- 单体生成
function New-FormPrefab {
    param(
        [string]$name,            # 根节点名 / 文件名
        [string]$scriptGuid,
        [string]$title,
        [hashtable[]]$buttons,    # @(@{ Field='m_ResumeBtn'; Label='继续'; Y=-10 })
        [float]$panelHeight,
        [float]$titleY,
        [float]$buttonStartY,
        [float]$buttonGap
    )

    New-Lines
    $base = [long]700000000000000000

    # ---- 根节点：全屏半透明遮罩 + 表单脚本 ----
    $rootGo = $base + 1
    $rootRect = $base + 2
    $rootCr = $base + 3
    $rootImg = $base + 4
    $rootGroup = $base + 5
    $rootScript = $base + 6

    # ---- 面板 ----
    $panelGo = $base + 11
    $panelRect = $base + 12
    $panelCr = $base + 13
    $panelImg = $base + 14

    # ---- 标题 ----
    $titleGo = $base + 21
    $titleRect = $base + 22
    $titleCr = $base + 23
    $titleText = $base + 24

    $buttonRoots = @()
    $buttonDefs = @()
    for ($i = 0; $i -lt $buttons.Count; $i++) {
        $offset = 31 + ($i * 10)
        $bGo = $base + $offset
        $bRect = $base + $offset + 1
        $bCr = $base + $offset + 2
        $bImg = $base + $offset + 3
        $bBtn = $base + $offset + 4
        $lGo = $base + $offset + 5
        $lRect = $base + $offset + 6
        $lCr = $base + $offset + 7
        $lText = $base + $offset + 8

        $buttonRoots += $bRect
        $buttonDefs += [pscustomobject]@{
            Go = $bGo; Rect = $bRect; Cr = $bCr; Img = $bImg; Btn = $bBtn
            LabelGo = $lGo; LabelRect = $lRect; LabelCr = $lCr; LabelText = $lText
            Field = $buttons[$i].Field
            Label = $buttons[$i].Label
            Y = $buttonStartY + ($i * $buttonGap)
        }
    }

    # 图层顺序：遮罩 → 面板 → 标题 → 按钮
    $panelChildren = @($titleRect) + $buttonRoots

    Add-GameObject -id $rootGo -name $name -components @($rootRect, $rootCr, $rootImg, $rootGroup, $rootScript)
    Add-RectTransform -id $rootRect -goId $rootGo -fatherId 0 -children @($panelRect) `
        -anchorMin '{x: 0, y: 0}' -anchorMax '{x: 1, y: 1}' -sizeDelta '{x: 0, y: 0}'
    Add-CanvasRenderer -id $rootCr -goId $rootGo
    Add-Image -id $rootImg -goId $rootGo -color '{r: 0, g: 0, b: 0, a: 0.7}'
    Add-CanvasGroup -id $rootGroup -goId $rootGo
    Add-FormScript -id $rootScript -goId $rootGo -scriptGuid $scriptGuid -fields @(
        ($buttonDefs | ForEach-Object { "$($_.Field): {fileID: $($_.Btn)}" })
    )

    Add-GameObject -id $panelGo -name 'Panel' -components @($panelRect, $panelCr, $panelImg)
    Add-RectTransform -id $panelRect -goId $panelGo -fatherId $rootRect -children $panelChildren `
        -sizeDelta (Vec2 720 $panelHeight)
    Add-CanvasRenderer -id $panelCr -goId $panelGo
    Add-Image -id $panelImg -goId $panelGo -color '{r: 0.12, g: 0.12, b: 0.14, a: 0.95}'

    Add-GameObject -id $titleGo -name 'TitleText' -components @($titleRect, $titleCr, $titleText)
    Add-RectTransform -id $titleRect -goId $titleGo -fatherId $panelRect -children @() `
        -sizeDelta '{x: 640, y: 90}' -anchoredPosition (Vec2 0 $titleY)
    Add-CanvasRenderer -id $titleCr -goId $titleGo
    Add-Text -id $titleText -goId $titleGo -text $title -fontSize 54

    foreach ($b in $buttonDefs) {
        Add-GameObject -id $b.Go -name ('Btn_' + $b.Field.Replace('m_', '').Replace('Btn', '')) -components @($b.Rect, $b.Cr, $b.Img, $b.Btn)
        Add-RectTransform -id $b.Rect -goId $b.Go -fatherId $panelRect -children @($b.LabelRect) `
            -sizeDelta '{x: 360, y: 84}' -anchoredPosition (Vec2 0 $b.Y)
        Add-CanvasRenderer -id $b.Cr -goId $b.Go
        Add-Image -id $b.Img -goId $b.Go -color '{r: 0.86, g: 0.86, b: 0.86, a: 1}'
        Add-Button -id $b.Btn -goId $b.Go -targetGraphicId $b.Img

        Add-GameObject -id $b.LabelGo -name 'Text' -components @($b.LabelRect, $b.LabelCr, $b.LabelText)
        Add-RectTransform -id $b.LabelRect -goId $b.LabelGo -fatherId $b.Rect -children @() `
            -anchorMin '{x: 0, y: 0}' -anchorMax '{x: 1, y: 1}' -sizeDelta '{x: 0, y: 0}'
        Add-CanvasRenderer -id $b.LabelCr -goId $b.LabelGo
        Add-Text -id $b.LabelText -goId $b.LabelGo -text $b.Label -fontSize 32 `
            -color '{r: 0.1, g: 0.1, b: 0.1, a: 1}'
    }

    return ("%YAML 1.1`r`n%TAG !u! tag:unity3d.com,2011:`r`n" + (($script:__buf -join "`r`n")) + "`r`n")
}

# ---------------------------------------------------------------- 三个界面
$specs = @(
    @{
        Name = 'PauseForm'; Script = $scriptIds.Pause; Title = '暂停'
        PanelHeight = 460; TitleY = 150; ButtonStartY = -10; ButtonGap = -110
        Buttons = @(
            @{ Field = 'm_ResumeBtn'; Label = '继续' },
            @{ Field = 'm_MainMenuBtn'; Label = '返回主菜单' }
        )
    },
    @{
        Name = 'DeathForm'; Script = $scriptIds.Death; Title = '你死了'
        PanelHeight = 360; TitleY = 95; ButtonStartY = -55; ButtonGap = -110
        Buttons = @(
            @{ Field = 'm_ReturnBtn'; Label = '返回主菜单' }
        )
    },
    @{
        Name = 'VictoryForm'; Script = $scriptIds.Victory; Title = '胜利'
        PanelHeight = 360; TitleY = 95; ButtonStartY = -55; ButtonGap = -110
        Buttons = @(
            @{ Field = 'm_ContinueBtn'; Label = '继续' }
        )
    }
)

foreach ($s in $specs) {
    $yaml = New-FormPrefab -name $s.Name -scriptGuid $s.Script -title $s.Title `
        -buttons $s.Buttons -panelHeight $s.PanelHeight -titleY $s.TitleY `
        -buttonStartY $s.ButtonStartY -buttonGap $s.ButtonGap

    $dir = Join-Path 'Assets/UI/Prefabs' ($s.Name -replace 'Form$', '')
    $path = Join-Path $dir ($s.Name + '.prefab')
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $path), $yaml, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "wrote $path ($(($yaml -split "`r`n").Count) lines)"
}
