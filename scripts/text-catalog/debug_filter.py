"""Source-based exclusions for UI reachable only through development tools.

Gameplay tools may also be enabled in Release with the explicit debug override.
Do not remove a shared text merely because another occurrence is a debug label.
In particular, the independent police button is available in forbidden ponds.
"""
import re

DEBUG_FILES = {
    "DesktopGameplayDebugMenu.cs", "GameplayDebugGate.cs", "gameplayDebug.ts",
    "fishingDebug.ts", "debugSampler.ts",
}
OVERLAY_INSPECT_METHODS = {
    "CreateInspectPlaceholder", "BuildFishInspectItems", "BuildSpotStatsInspectItems",
    "FormatSpotOverviewDetail", "FormatSpotConstantsDetail", "FormatSpotBiteDetail",
    "FormatSpotContributionDetail", "FormatDebugFishDetail", "FormatQualityLabel",
}
SETTINGS_SERVER_METHODS = {
    "EnsureServerUrlControls", "RefreshServerUrlControls", "OnServerUrlEdited",
    "SyncServerPlaceholder", "SaveServerUrl", "TestServerUrl", "HealthCheckRoutine",
}
SETTINGS_CONTROL_NAMES = {
    "模拟连接错误", "服务器", "当前服务器", "服务器地址输入", "保存服务器地址",
    "测试服务器连接", "服务器连接状态",
}
SETTINGS_TEXT = SETTINGS_CONTROL_NAMES | {
    "服务器连接", "服务器地址", "当前服务器：", "服务器地址输入/Placeholder",
    "服务器地址输入/Text", "http://公网或局域网IP:3001", "一键出警（Debug）",
}


def debug_control(name):
    # PoliceDebugButton / btn_debug_police are deliberately retained.
    return bool(re.search(r"GameplayDebug|btn_debug_gameplay", name))


def excluded_source(path, value, ctx="", location="", node_debug=False):
    p = str(path)
    filename = p.rsplit("/", 1)[-1]
    if re.match(r'\s*(?:(?:System\.Diagnostics|UnityEngine)\.)?(?:Debug\.(?:Log(?:Warning|Error|Exception|Assertion)?(?:Format)?|WriteLine)|console\.(?:log|warn|error|debug|info|trace)|Console\.Write(?:Line)?|logger\.(?:log|warn|error|debug|info|trace)|LatencyTrace\.Write|Trace\.Write(?:Line)?)\s*\(', ctx):
        return 'Debug与日志输出'
    if filename in DEBUG_FILES:
        return "调试专用文件"
    if node_debug:
        return "调试分支或控件"
    if debug_control(location):
        return "玩法调试控件"
    if re.search(r"GameplayDebug|OverlayDebug|RefreshInventoryAfterDebug", location):
        return "玩法调试方法"
    if filename == "MainWindow.xaml.cs" and location in OVERLAY_INSPECT_METHODS:
        return "玩法调试数据面板"
    if filename == "AuthenticatedApiClient.cs" and location == "FormatSpotStatsReport":
        return "玩法调试数据报告"
    if filename == "DesktopNotificationService.cs" and location == "PublishSimulated":
        return "调试模拟通知"
    if filename == "DesktopSettingsModalView.cs" and location in SETTINGS_SERVER_METHODS:
        return "Debug服务器设置页"
    if filename == "DesktopServerConfig.cs" and location == "TryWriteServerBaseUrl":
        return "Debug服务器设置保存"
    # These labels name controls that BindTabs/ShowTab hide in non-debug builds.
    if value in SETTINGS_TEXT:
        return "Debug设置控件文案"
    if re.search(r"(?:^|/)SettingsTabs/Server(?:/|$)", location):
        return "Debug服务器设置页"
    if filename == "metrics-schema.ts" and ('gameplay_debug' in ctx or value.startswith("玩法 Debug 菜单操作")):
        return "调试事件说明"
    # Compile-time Debug build entries are editor tooling, not production copy.
    if filename == "DesktopBuildMenu.cs" and (location == "BuildWindowsDevelopment" or value == "Debug" or value.startswith("Debug 包")):
        return "Debug打包入口"
    return None


def code_node_is_debug(node, member, filename, node_text):
    """Detect gated subtrees in otherwise shared methods, preserving other arms."""
    current = node.parent
    while current is not None:
        if current.type == "if_statement":
            condition = current.child_by_field_name("condition")
            consequence = current.child_by_field_name("consequence")
            alternative = current.child_by_field_name("alternative")
            if condition is not None:
                expr = node_text(condition)
                in_then = consequence is not None and consequence.start_byte <= node.start_byte < consequence.end_byte
                in_else = alternative is not None and alternative.start_byte <= node.start_byte < alternative.end_byte
                if "_pendingDebugCommandId" in expr and in_then:
                    return True
                if "Debug.isDebugBuild" in expr:
                    negated = bool(re.match(r"\s*!\s*Debug\.isDebugBuild", expr))
                    if (in_then and not negated) or (in_else and negated):
                        return True
                if "btn_debug_gameplay" in expr and in_then:
                    return True
        if filename == "DesktopSettingsModalView.cs" and member == "RefreshTabInfo" and current.type == "switch_section":
            if re.match(r"\s*case\s+2\s*:", node_text(current)):
                return True
        current = current.parent
    return False


def unity_debug_documents(documents, object_ids):
    """Resolve local GameObject/Transform ancestry and prefab instance overrides."""
    def mapping(node):
        return {k.value: v for k, v in node.value} if node is not None and isinstance(node.value, list) and node.id == "mapping" else {}
    def scalar(node, default=""):
        return node.value if node is not None and node.id == "scalar" else default
    def ref(node):
        return scalar(mapping(node).get("fileID"), "0")
    roots = []
    names = {}
    transforms = {}
    go_transforms = {}
    debug_instances = set()
    for object_id, node in zip(object_ids, documents):
        outer = mapping(node)
        kind, inner = next(iter(outer.items())) if outer else ("", None)
        fields = mapping(inner)
        roots.append((object_id, kind, fields))
        if kind == "GameObject":
            names[object_id] = scalar(fields.get("m_Name"))
        if kind in {"Transform", "RectTransform"}:
            go = ref(fields.get("m_GameObject"))
            transforms[object_id] = (go, ref(fields.get("m_Father")))
            go_transforms[go] = object_id
        if kind == "PrefabInstance":
            mods = mapping(fields.get("m_Modification")).get("m_Modifications")
            if mods is not None:
                for mod in mods.value:
                    fields_mod = mapping(mod)
                    if scalar(fields_mod.get("propertyPath")) == "m_Name":
                        name = scalar(fields_mod.get("value"))
                        if name in SETTINGS_CONTROL_NAMES or name == "Server" or debug_control(name):
                            debug_instances.add(object_id)
    def is_debug_go(go):
        seen = set()
        while go not in seen and go != "0":
            seen.add(go)
            name = names.get(go, "")
            if name in SETTINGS_CONTROL_NAMES or name == "Server" or debug_control(name):
                return True
            trans = go_transforms.get(go)
            if trans is None:
                break
            father = transforms[trans][1]
            go = transforms.get(father, ("0", "0"))[0]
        return False
    excluded = set()
    for index, (object_id, kind, fields) in enumerate(roots):
        if object_id in debug_instances or ref(fields.get("m_PrefabInstance")) in debug_instances or is_debug_go(object_id if kind == "GameObject" else ref(fields.get("m_GameObject"))):
            excluded.add(index)
    return excluded
