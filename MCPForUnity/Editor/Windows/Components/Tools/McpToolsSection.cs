using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Windows.Components.Tools
{
    /// <summary>
    /// Controller for the Tools section inside the MCP for Unity editor window.
    /// Provides discovery, filtering, and per-tool enablement toggles.
    /// Tools are grouped by their Group property (core first, then alphabetical).
    /// </summary>
    public class McpToolsSection
    {
        private readonly Dictionary<string, Toggle> toolToggleMap = new();
        private Toggle projectScopedToolsToggle;
        private Label summaryLabel;
        private Label noteLabel;
        private Button enableAllButton;
        private Button disableAllButton;
        private Button rescanButton;
        private Button reconfigureButton;
        private Button expandAllButton;
        private Button collapseAllButton;
        private ToolbarSearchField toolSearchField;
        private Toggle enabledOnlyToggle;
        private Label filterEmptyLabel;
        private VisualElement categoryContainer;
        private List<ToolMetadata> allTools = new();
        private readonly Dictionary<string, VisualElement> toolRowMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Toggle> groupToggleMap = new();
        private readonly List<(Foldout foldout, string title, List<ToolMetadata> tools)> foldoutEntries = new();

        /// <summary>Human-friendly names for tool groups shown in the UI.</summary>
        private static readonly Dictionary<string, string> GroupDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "core", "Core Tools" },
            { "docs", "Unity API Documentation" },
            { "vfx", "VFX & Shaders" },
            { "animation", "Animation" },
            { "ui", "UI Toolkit" },
            { "scripting_ext", "Scripting Extensions" },
            { "testing", "Testing" },
            { "probuilder", "ProBuilder — Experimental" },
            { "profiling", "Profiling & Frame Debugger" },
            { "asset_gen", "Asset Gen" },
        };

        public VisualElement Root { get; }

        public McpToolsSection(VisualElement root)
        {
            Root = root;
            CacheUIElements();
            RegisterCallbacks();
        }

        private void CacheUIElements()
        {
            projectScopedToolsToggle = Root.Q<Toggle>("project-scoped-tools-toggle");
            summaryLabel = Root.Q<Label>("tools-summary");
            noteLabel = Root.Q<Label>("tools-note");
            enableAllButton = Root.Q<Button>("enable-all-button");
            disableAllButton = Root.Q<Button>("disable-all-button");
            rescanButton = Root.Q<Button>("rescan-button");
            reconfigureButton = Root.Q<Button>("reconfigure-button");
            expandAllButton = Root.Q<Button>("expand-all-button");
            collapseAllButton = Root.Q<Button>("collapse-all-button");
            toolSearchField = Root.Q<ToolbarSearchField>("tool-search-field");
            enabledOnlyToggle = Root.Q<Toggle>("enabled-only-toggle");
            categoryContainer = Root.Q<VisualElement>("tool-category-container");
        }

        private void RegisterCallbacks()
        {
            if (toolSearchField != null)
            {
                toolSearchField.tooltip = "Search tool names, descriptions, groups, and parameters.";
                toolSearchField.RegisterValueChangedCallback(_ => ApplyFilters());
            }

            if (enabledOnlyToggle != null)
            {
                enabledOnlyToggle.tooltip = "Show only tools currently enabled for registration.";
                enabledOnlyToggle.RegisterValueChangedCallback(_ => ApplyFilters());
            }

            if (expandAllButton != null)
            {
                expandAllButton.clicked += () => SetAllFoldoutsExpanded(true);
            }

            if (collapseAllButton != null)
            {
                collapseAllButton.clicked += () => SetAllFoldoutsExpanded(false);
            }

            if (projectScopedToolsToggle != null)
            {
                projectScopedToolsToggle.value = EditorPrefs.GetBool(
                    EditorPrefKeys.ProjectScopedToolsLocalHttp,
                    false
                );
                projectScopedToolsToggle.tooltip = "When enabled, register project-scoped tools with HTTP Local and stdio transports. Allows per-project tool customization.";
                projectScopedToolsToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(EditorPrefKeys.ProjectScopedToolsLocalHttp, evt.newValue);
                });
            }

            if (enableAllButton != null)
            {
                enableAllButton.AddToClassList("tool-action-button");
                enableAllButton.style.marginRight = 4;
                enableAllButton.clicked += () => SetAllToolsState(true);
            }

            if (disableAllButton != null)
            {
                disableAllButton.AddToClassList("tool-action-button");
                disableAllButton.style.marginRight = 4;
                disableAllButton.clicked += () => SetAllToolsState(false);
            }

            if (rescanButton != null)
            {
                rescanButton.AddToClassList("tool-action-button");
                rescanButton.clicked += () =>
                {
                    McpLog.Info("Rescanning MCP tools from the editor window.");
                    MCPServiceLocator.ToolDiscovery.InvalidateCache();
                    Refresh();
                };
            }

            if (reconfigureButton != null)
            {
                reconfigureButton.AddToClassList("tool-action-button");
                reconfigureButton.clicked += OnReconfigureClientsClicked;
            }
        }

        /// <summary>
        /// Rebuilds the tool list and synchronises toggle states.
        /// Tools are displayed in group-based foldouts: core first, then other
        /// groups alphabetically. Custom (non-built-in) tools appear in a
        /// separate "Custom Tools" foldout at the bottom.
        /// </summary>
        public void Refresh()
        {
            toolToggleMap.Clear();
            groupToggleMap.Clear();
            foldoutEntries.Clear();
            toolRowMap.Clear();
            filterEmptyLabel = null;
            categoryContainer?.Clear();

            var service = MCPServiceLocator.ToolDiscovery;
            allTools = service.DiscoverAllTools()
                .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasTools = allTools.Count > 0;
            enableAllButton?.SetEnabled(hasTools);
            disableAllButton?.SetEnabled(hasTools);

            if (noteLabel != null)
            {
                noteLabel.style.display = hasTools ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasTools)
                {
                    bool isHttp = EditorConfigurationCache.Instance.UseHttpTransport;
                    noteLabel.text = isHttp
                        ? "Changes apply after reconnecting or re-registering tools."
                        : "Stdio mode: toggles sync at startup. After changing toggles, ask the AI to run manage_tools with action 'sync' to refresh.";
                }
            }

            if (!hasTools)
            {
                AddInfoLabel("No MCP tools found. Add classes decorated with [McpForUnityTool] to expose tools.");
                UpdateSummary();
                return;
            }

            // Partition into built-in and custom
            var builtInTools = allTools.Where(IsBuiltIn).ToList();
            var customTools = allTools.Where(tool => !IsBuiltIn(tool)).ToList();

            // Group built-in tools by their Group property
            var grouped = builtInTools
                .GroupBy(t => t.Group ?? "core")
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList());

            // Render "core" first, then remaining groups alphabetically
            if (grouped.TryGetValue("core", out var coreTools))
            {
                BuildCategory(GetGroupDisplayName("core"), "group-core", coreTools);
                grouped.Remove("core");
            }

            foreach (var kvp in grouped.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                BuildCategory(GetGroupDisplayName(kvp.Key), $"group-{kvp.Key}", kvp.Value);
            }

            // Custom tools at the bottom
            if (customTools.Count > 0)
            {
                BuildCategory("Custom Tools", "custom", customTools);
            }

            ApplyFilters();
        }

        private static string GetGroupDisplayName(string group)
        {
            if (GroupDisplayNames.TryGetValue(group, out var displayName))
                return displayName;
            // Fallback: capitalize first letter
            return string.IsNullOrEmpty(group)
                ? "Other"
                : char.ToUpper(group[0]) + group.Substring(1);
        }

        private void BuildCategory(string title, string prefsSuffix, IEnumerable<ToolMetadata> tools)
        {
            var toolList = tools.ToList();
            if (toolList.Count == 0)
            {
                return;
            }

            bool isExperimental = string.Equals(prefsSuffix, "group-probuilder", StringComparison.OrdinalIgnoreCase);

            int enabledCount = toolList.Count(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));

            // Default foldout state: core is open, others collapsed
            bool defaultOpen = prefsSuffix == "group-core";
            var foldout = new Foldout
            {
                text = $"{title} ({enabledCount}/{toolList.Count})",
                value = EditorPrefs.GetBool(EditorPrefKeys.ToolFoldoutStatePrefix + prefsSuffix, defaultOpen)
            };

            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout) return;
                EditorPrefs.SetBool(EditorPrefKeys.ToolFoldoutStatePrefix + prefsSuffix, evt.newValue);
            });

            // Add a checkbox into the foldout header to toggle all tools in this group
            bool allEnabled = enabledCount == toolList.Count;
            var groupCheckbox = new Toggle { value = allEnabled };
            groupCheckbox.AddToClassList("group-header-checkbox");
            groupCheckbox.tooltip = $"Toggle all tools in \"{title}\" on or off.";

            // Prevent the click from propagating to the foldout expand/collapse toggle
            groupCheckbox.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            groupCheckbox.RegisterValueChangedCallback(evt =>
            {
                evt.StopPropagation();
                SetGroupToolsState(toolList, evt.newValue, foldout, title);
            });

            // Insert the checkbox into the foldout's own header toggle element
            foldout.Q<Toggle>()?.Add(groupCheckbox);
            groupToggleMap[prefsSuffix] = groupCheckbox;

            foreach (var tool in toolList)
            {
                var row = CreateToolRow(tool);
                foldout.Add(row);
                toolRowMap[tool.Name] = row;
            }

            if (isExperimental)
            {
                var warning = new HelpBox(
                    "ProBuilder support is experimental. Mesh editing operations may produce " +
                    "unexpected results on complex topologies. Always save your scene before " +
                    "performing destructive operations.",
                    HelpBoxMessageType.Warning);
                warning.style.marginTop = 4;
                warning.style.marginBottom = 2;
                foldout.Insert(0, warning);
            }

            foldoutEntries.Add((foldout, title, toolList));
            categoryContainer?.Add(foldout);
        }

        private VisualElement CreateToolRow(ToolMetadata tool)
        {
            var row = new VisualElement();
            row.AddToClassList("tool-item");

            var header = new VisualElement();
            header.AddToClassList("tool-item-header");

            var toggle = new Toggle(tool.Name)
            {
                value = MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name)
            };
            toggle.AddToClassList("tool-item-toggle");
            toggle.tooltip = string.IsNullOrWhiteSpace(tool.Description) ? tool.Name : tool.Description;

            toggle.RegisterValueChangedCallback(evt =>
            {
                HandleToggleChange(tool, evt.newValue);
            });

            toolToggleMap[tool.Name] = toggle;
            header.Add(toggle);

            var tagsContainer = new VisualElement();
            tagsContainer.AddToClassList("tool-tags");

            bool defaultEnabled = tool.AutoRegister || tool.IsBuiltIn;
            tagsContainer.Add(CreateTag(defaultEnabled ? "On by default" : "Off by default"));

            tagsContainer.Add(CreateTag(tool.StructuredOutput ? "Structured output" : "Free-form"));

            if (tool.RequiresPolling)
            {
                tagsContainer.Add(CreateTag($"Polling: {tool.PollAction}"));
            }

            header.Add(tagsContainer);
            row.Add(header);

            // Skip auto-generated placeholder descriptions like "Tool: find_gameobjects"
            if (!string.IsNullOrWhiteSpace(tool.Description)
                && !tool.Description.StartsWith("Tool: ", StringComparison.OrdinalIgnoreCase))
            {
                var description = new Label(tool.Description);
                description.AddToClassList("tool-item-description");
                row.Add(description);
            }

            if (tool.Parameters != null && tool.Parameters.Count > 0)
            {
                var paramSummary = string.Join(", ", tool.Parameters.Select(p =>
                    $"{p.Name}{(p.Required ? string.Empty : " (optional)")}: {p.Type}"));

                var parametersLabel = new Label(paramSummary);
                parametersLabel.AddToClassList("tool-parameters");
                row.Add(parametersLabel);
            }

            if (IsManageCameraTool(tool))
            {
                row.Add(CreateManageSceneActions());
            }

            if (IsBatchExecuteTool(tool))
            {
                row.Add(CreateBatchExecuteSettings());
            }

            if (string.Equals(tool.Name, "manage_editor", StringComparison.OrdinalIgnoreCase))
            {
                row.Add(CreateGameViewSizeControls());
            }

            if (string.Equals(tool.Name, "manage_editor_windows", StringComparison.OrdinalIgnoreCase))
            {
                row.Add(CreateEditorWindowCaptureControls());
            }

            return row;
        }

        private void HandleToggleChange(
            ToolMetadata tool,
            bool enabled,
            bool updateSummary = true,
            bool reregisterTools = true)
        {
            MCPServiceLocator.ToolDiscovery.SetToolEnabled(tool.Name, enabled);

            if (updateSummary)
            {
                UpdateFoldoutHeaders();
                SyncGroupToggles();
                ApplyFilters();
            }

            if (reregisterTools)
            {
                // Trigger tool reregistration with connected MCP server
                ReregisterToolsAsync();
            }
        }

        private void ReregisterToolsAsync()
        {
            // Fire and forget - don't block UI thread
            var transportManager = MCPServiceLocator.TransportManager;
            var client = transportManager.GetClient(TransportMode.Http);
            if (client == null || !client.IsConnected)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await client.ReregisterToolsAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"Failed to reregister tools: {ex}");
                }
            });
        }

        private void SetAllToolsState(bool enabled)
        {
            bool hasChanges = false;

            foreach (var tool in allTools)
            {
                if (!toolToggleMap.TryGetValue(tool.Name, out var toggle))
                {
                    bool currentEnabled = MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name);
                    if (currentEnabled != enabled)
                    {
                        MCPServiceLocator.ToolDiscovery.SetToolEnabled(tool.Name, enabled);
                        hasChanges = true;
                    }
                    continue;
                }

                if (toggle.value == enabled)
                {
                    continue;
                }

                toggle.SetValueWithoutNotify(enabled);
                HandleToggleChange(tool, enabled, updateSummary: false, reregisterTools: false);
                hasChanges = true;
            }

            UpdateFoldoutHeaders();
            SyncGroupToggles();
            ApplyFilters();

            if (hasChanges)
            {
                // Trigger a single reregistration after bulk change
                ReregisterToolsAsync();
            }
        }

        private void SetGroupToolsState(List<ToolMetadata> groupTools, bool enabled, Foldout foldout, string title)
        {
            bool hasChanges = false;

            foreach (var tool in groupTools)
            {
                if (toolToggleMap.TryGetValue(tool.Name, out var toggle))
                {
                    if (toggle.value != enabled)
                    {
                        toggle.SetValueWithoutNotify(enabled);
                        HandleToggleChange(tool, enabled, updateSummary: false, reregisterTools: false);
                        hasChanges = true;
                    }
                }
                else
                {
                    bool currentEnabled = MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name);
                    if (currentEnabled != enabled)
                    {
                        MCPServiceLocator.ToolDiscovery.SetToolEnabled(tool.Name, enabled);
                        hasChanges = true;
                    }
                }
            }

            // Update the foldout header count
            int enabledCount = groupTools.Count(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));
            foldout.text = $"{title} ({enabledCount}/{groupTools.Count})";

            // Sync global group toggles after group change
            SyncGroupToggles();

            ApplyFilters();

            if (hasChanges)
            {
                ReregisterToolsAsync();
            }
        }

        /// <summary>
        /// Synchronises group toggle checkmarks with actual tool states.
        /// Called after individual tool toggles change so the group toggle
        /// stays accurate.
        /// </summary>
        private void SyncGroupToggles()
        {
            // We need the grouped tool lists to check states.
            var builtInTools = allTools.Where(IsBuiltIn).ToList();
            var grouped = builtInTools
                .GroupBy(t => t.Group ?? "core")
                .ToDictionary(g => g.Key, g => g.ToList());
            var customTools = allTools.Where(t => !IsBuiltIn(t)).ToList();

            foreach (var kvp in groupToggleMap)
            {
                List<ToolMetadata> groupTools;
                if (kvp.Key == "custom")
                {
                    groupTools = customTools;
                }
                else
                {
                    string groupKey = kvp.Key.StartsWith("group-") ? kvp.Key.Substring(6) : kvp.Key;
                    if (!grouped.TryGetValue(groupKey, out groupTools))
                        continue;
                }

                bool allEnabled = groupTools.All(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));
                kvp.Value.SetValueWithoutNotify(allEnabled);
            }
        }

        private void OnReconfigureClientsClicked()
        {
            try
            {
                // Re-register tools with the server (HTTP mode)
                ReregisterToolsAsync();

                // Reconfigure all already-configured clients.
                // For CLI-based clients Configure() is a toggle (unregister if
                // configured, register if not), so we call it twice: first to
                // unregister, then to re-register with the updated tool set.
                var clients = MCPServiceLocator.Client.GetAllClients();
                int success = 0;
                int skipped = 0;
                var messages = new List<string>();

                foreach (var client in clients)
                {
                    try
                    {
                        client.CheckStatus(attemptAutoRewrite: false);

                        if (client.Status != McpStatus.Configured)
                        {
                            skipped++;
                            continue;
                        }

                        if (client is ClaudeCliMcpConfigurator)
                        {
                            // Toggle off (unregister), then toggle on (register)
                            MCPServiceLocator.Client.ConfigureClient(client);
                            MCPServiceLocator.Client.ConfigureClient(client);
                        }
                        else
                        {
                            // JSON-file clients: rewrite is idempotent
                            MCPServiceLocator.Client.ConfigureClient(client);
                        }

                        success++;
                        messages.Add($"✓ {client.DisplayName}: Reconfigured");
                    }
                    catch (Exception ex)
                    {
                        messages.Add($"⚠ {client.DisplayName}: {ex.Message}");
                    }
                }

                string header = $"Reconfigured {success} client(s), skipped {skipped}.";
                string body = messages.Count > 0
                    ? header + "\n\n" + string.Join("\n", messages)
                    : header;

                EditorUtility.DisplayDialog("Reconfigure Clients", body, "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Reconfigure Failed", ex.Message, "OK");
                McpLog.Error($"Reconfigure failed: {ex.Message}");
            }
        }

        private void SetAllFoldoutsExpanded(bool expanded)
        {
            foreach (var (foldout, _, _) in foldoutEntries)
            {
                foldout.value = expanded;
            }
        }

        private bool MatchesToolFilter(ToolMetadata tool)
        {
            if (enabledOnlyToggle != null && enabledOnlyToggle.value
                && !MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name))
            {
                return false;
            }

            string query = toolSearchField?.value?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            string parameters = tool.Parameters == null
                ? string.Empty
                : string.Join(" ", tool.Parameters.Select(parameter =>
                    $"{parameter.Name} {parameter.Type} {parameter.Description}"));

            string group = tool.Group ?? "core";
            string searchable = string.Join(" ", new[]
            {
                tool.Name,
                tool.Description,
                group,
                GetGroupDisplayName(group),
                parameters
            });

            return searchable.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ApplyFilters()
        {
            if (categoryContainer == null)
            {
                UpdateSummary();
                return;
            }

            string query = toolSearchField?.value?.Trim();
            bool filterActive = !string.IsNullOrEmpty(query)
                || (enabledOnlyToggle != null && enabledOnlyToggle.value);
            int visibleCount = 0;

            foreach (var tool in allTools)
            {
                bool matches = MatchesToolFilter(tool);
                if (matches)
                {
                    visibleCount++;
                }

                if (toolRowMap.TryGetValue(tool.Name, out var row))
                {
                    row.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            foreach (var (foldout, _, tools) in foldoutEntries)
            {
                bool hasMatches = tools.Any(MatchesToolFilter);
                foldout.style.display = hasMatches ? DisplayStyle.Flex : DisplayStyle.None;
                if (filterActive && hasMatches)
                {
                    foldout.SetValueWithoutNotify(true);
                }
            }

            if (filterEmptyLabel == null)
            {
                filterEmptyLabel = new Label();
                filterEmptyLabel.AddToClassList("help-text");
                filterEmptyLabel.AddToClassList("tool-editor-status");
                categoryContainer.Add(filterEmptyLabel);
            }

            filterEmptyLabel.text = filterActive
                ? "No tools match the current search and filter."
                : "No MCP tools were found.";
            filterEmptyLabel.style.display = allTools.Count > 0 && visibleCount == 0
                ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (summaryLabel == null)
            {
                return;
            }

            if (allTools.Count == 0)
            {
                summaryLabel.text = "No MCP tools discovered.";
                return;
            }

            int enabledCount = allTools.Count(tool => MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name));
            bool filterActive = !string.IsNullOrWhiteSpace(toolSearchField?.value)
                || (enabledOnlyToggle != null && enabledOnlyToggle.value);
            if (filterActive)
            {
                int visibleCount = allTools.Count(MatchesToolFilter);
                summaryLabel.text = $"{enabledCount} of {allTools.Count} tools enabled; showing {visibleCount} matching tool(s).";
            }
            else
            {
                summaryLabel.text = $"{enabledCount} of {allTools.Count} tools will register with connected clients.";
            }
        }

        private void UpdateFoldoutHeaders()
        {
            foreach (var (foldout, title, tools) in foldoutEntries)
            {
                int enabledCount = tools.Count(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));
                foldout.text = $"{title} ({enabledCount}/{tools.Count})";
            }
        }

        private void AddInfoLabel(string message)
        {
            var label = new Label(message);
            label.AddToClassList("help-text");
            categoryContainer?.Add(label);
        }

        private VisualElement CreateGameViewSizeControls()
        {
            const string preferenceKey = "MCPForUnity.ToolsSection.FixedGameViewFoldout";
            var container = new VisualElement();
            container.AddToClassList("tool-editor-controls");

            var foldout = new Foldout
            {
                text = "Fixed Game View size",
                value = EditorPrefs.GetBool(preferenceKey, false)
            };
            foldout.AddToClassList("tool-editor-control-foldout");
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(preferenceKey, evt.newValue));
            container.Add(foldout);

            var inputRow = new VisualElement();
            inputRow.AddToClassList("tool-editor-control-row");

            var widthField = new IntegerField("Width") { value = 1920 };
            widthField.AddToClassList("tool-editor-size-field");
            widthField.tooltip = "Requested fixed Game View width in pixels (10–8192).";
            var heightField = new IntegerField("Height") { value = 1080 };
            heightField.AddToClassList("tool-editor-size-field");
            heightField.tooltip = "Requested fixed Game View height in pixels (10–8192).";
            inputRow.Add(widthField);
            inputRow.Add(heightField);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("tool-editor-control-row");

            var readButton = new Button { text = "Read Current" };
            readButton.AddToClassList("tool-editor-control-button");
            var applyButton = new Button { text = "Apply Size" };
            applyButton.AddToClassList("tool-editor-control-button");
            var status = new Label("Read the current preset or enter a custom size.");
            status.AddToClassList("tool-editor-status");

            readButton.clicked += () => _ = ReadGameViewSizeAsync(widthField, heightField, status, readButton, applyButton);
            applyButton.clicked += () => _ = ApplyGameViewSizeAsync(widthField, heightField, status, readButton, applyButton);

            buttonRow.Add(readButton);
            buttonRow.Add(applyButton);
            foldout.Add(inputRow);
            foldout.Add(buttonRow);
            foldout.Add(status);
            return container;
        }

        private async Task ReadGameViewSizeAsync(
            IntegerField widthField, IntegerField heightField, Label status, Button readButton, Button applyButton)
        {
            readButton.SetEnabled(false);
            applyButton.SetEnabled(false);
            try
            {
                object result = await ResolveCommandResultAsync(
                    ManageEditor.HandleCommand(new JObject { ["action"] = "get_game_view_size" }));

                if (result is SuccessResponse success)
                {
                    var data = success.Data as JObject
                        ?? (success.Data == null ? new JObject() : JObject.FromObject(success.Data));
                    var selected = data["selected_size"] as JObject;
                    if (selected != null)
                    {
                        widthField.SetValueWithoutNotify(selected["width"].Value<int>());
                        heightField.SetValueWithoutNotify(selected["height"].Value<int>());
                    }

                    string preset = data["preset_name"]?.Value<string>() ?? "custom";
                    string settled = data["settled"]?.Value<bool>() == true ? "settled" : "not settled";
                    status.text = "Preset " + preset + "; target " + FormatSize(data["target_size"])
                        + "; render target " + FormatSize(data["render_size"]) + " (" + settled + ").";
                }
                else if (result is ErrorResponse error)
                {
                    status.text = error.Error;
                }
                else
                {
                    status.text = "Unexpected response while reading Game View size.";
                }
            }
            catch (Exception ex)
            {
                status.text = "Could not read Game View size: " + ex.Message;
                McpLog.Warn(status.text);
            }
            finally
            {
                readButton.SetEnabled(true);
                applyButton.SetEnabled(true);
            }
        }

        private async Task ApplyGameViewSizeAsync(
            IntegerField widthField, IntegerField heightField, Label status, Button readButton, Button applyButton)
        {
            int width = widthField.value;
            int height = heightField.value;
            if (width < GameViewSizeControl.MinDimension || width > GameViewSizeControl.MaxDimension
                || height < GameViewSizeControl.MinDimension || height > GameViewSizeControl.MaxDimension)
            {
                status.text = $"Width and height must be between {GameViewSizeControl.MinDimension} and {GameViewSizeControl.MaxDimension} pixels.";
                return;
            }

            readButton.SetEnabled(false);
            applyButton.SetEnabled(false);
            status.text = "Applying fixed Game View size…";
            try
            {
                object result = await ResolveCommandResultAsync(ManageEditor.HandleCommand(new JObject
                {
                    ["action"] = "set_game_view_size",
                    ["width"] = width,
                    ["height"] = height
                }));

                if (result is SuccessResponse success)
                {
                    var data = success.Data as JObject;
                    string settled = data?["settled"]?.Value<bool>() == true ? "settled" : "waiting for render target";
                    status.text = success.Message + " " + width + "×" + height + " requested; " + settled + ".";
                    McpLog.Info(status.text);
                }
                else if (result is ErrorResponse error)
                {
                    status.text = error.Error;
                }
                else
                {
                    status.text = "Unexpected response while applying Game View size.";
                }
            }
            catch (Exception ex)
            {
                status.text = "Could not apply Game View size: " + ex.Message;
                McpLog.Error(status.text);
            }
            finally
            {
                readButton.SetEnabled(true);
                applyButton.SetEnabled(true);
            }
        }

        private VisualElement CreateEditorWindowCaptureControls()
        {
            const string preferenceKey = "MCPForUnity.ToolsSection.EditorWindowCaptureFoldout";
            var container = new VisualElement();
            container.AddToClassList("tool-editor-controls");

            var foldout = new Foldout
            {
                text = "Editor window screenshot",
                value = EditorPrefs.GetBool(preferenceKey, false)
            };
            foldout.AddToClassList("tool-editor-control-foldout");
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(preferenceKey, evt.newValue));
            container.Add(foldout);

            var idsByChoice = new Dictionary<string, int>();
            var selectionRow = new VisualElement();
            selectionRow.AddToClassList("tool-editor-control-row");

            var choices = new List<string> { "Refresh to discover windows" };
            var selector = new DropdownField("Target", choices, 0);
            selector.style.flexGrow = 1;
            selector.style.minWidth = 180;
            selectionRow.Add(selector);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("tool-editor-control-row");

            var refreshButton = new Button { text = "Refresh Windows" };
            refreshButton.AddToClassList("tool-editor-control-button");
            var captureButton = new Button { text = "Capture PNG" };
            captureButton.AddToClassList("tool-editor-control-button");
            captureButton.SetEnabled(false);
            var status = new Label("Choose an Editor window; capture saves a PNG under Library/McpEditorScreenshots.");
            status.AddToClassList("tool-editor-status");

            selector.RegisterValueChangedCallback(_ =>
                captureButton.SetEnabled(idsByChoice.ContainsKey(selector.value)));
            refreshButton.clicked += () => _ = RefreshEditorWindowChoicesAsync(selector, idsByChoice, status, refreshButton, captureButton);
            captureButton.clicked += () => _ = CaptureEditorWindowAsync(selector, idsByChoice, status, refreshButton, captureButton);

            buttonRow.Add(refreshButton);
            buttonRow.Add(captureButton);
            foldout.Add(selectionRow);
            foldout.Add(buttonRow);
            foldout.Add(status);
            return container;
        }

        private static async Task RefreshEditorWindowChoicesAsync(
            DropdownField selector, Dictionary<string, int> idsByChoice, Label status, Button refreshButton, Button captureButton)
        {
            refreshButton.SetEnabled(false);
            captureButton.SetEnabled(false);
            try
            {
                object result = await ResolveCommandResultAsync(
                    ManageEditorWindows.HandleCommand(new JObject { ["action"] = "list" }));
                if (!(result is SuccessResponse success))
                {
                    status.text = result is ErrorResponse error ? error.Error : "Could not list Editor windows.";
                    return;
                }

                var data = success.Data as JObject
                    ?? (success.Data == null ? new JObject() : JObject.FromObject(success.Data));
                var windows = data["windows"] as JArray;
                var choices = new List<string>();
                idsByChoice.Clear();

                if (windows != null)
                {
                    foreach (var token in windows.OfType<JObject>())
                    {
                        int? id = token["window_id"]?.Value<int?>();
                        string title = token["title"]?.Value<string>();
                        if (!id.HasValue || string.IsNullOrWhiteSpace(title)) continue;

                        string choice = title + " (ID " + id.Value + ")";
                        choices.Add(choice);
                        idsByChoice[choice] = id.Value;
                    }
                }

                if (choices.Count == 0)
                {
                    choices.Add("No eligible windows found");
                }

                selector.choices = choices;
                selector.SetValueWithoutNotify(choices[0]);
                captureButton.SetEnabled(idsByChoice.ContainsKey(selector.value));
                status.text = "Found " + idsByChoice.Count + " eligible Editor window(s). Captures are saved to Library/McpEditorScreenshots.";
            }
            catch (Exception ex)
            {
                status.text = "Could not list Editor windows: " + ex.Message;
                McpLog.Warn(status.text);
            }
            finally
            {
                refreshButton.SetEnabled(true);
                captureButton.SetEnabled(idsByChoice.ContainsKey(selector.value));
            }
        }

        private static async Task CaptureEditorWindowAsync(
            DropdownField selector, Dictionary<string, int> idsByChoice, Label status, Button refreshButton, Button captureButton)
        {
            if (!idsByChoice.TryGetValue(selector.value, out int windowId))
            {
                status.text = "Select an Editor window first.";
                return;
            }

            refreshButton.SetEnabled(false);
            captureButton.SetEnabled(false);
            status.text = "Capturing Editor window…";
            try
            {
                object result = await ResolveCommandResultAsync(ManageEditorWindows.HandleCommand(new JObject
                {
                    ["action"] = "screenshot",
                    ["window_id"] = windowId,
                    ["focus"] = true,
                    ["restore_focus"] = true,
                    ["include_image"] = false,
                    ["save_file"] = true
                }));

                if (result is SuccessResponse success)
                {
                    var data = success.Data as JObject
                        ?? (success.Data == null ? new JObject() : JObject.FromObject(success.Data));
                    var window = data["window"] as JObject;
                    string title = window?["title"]?.Value<string>() ?? selector.value;
                    string path = data["path"]?.Value<string>() ?? "Library/McpEditorScreenshots";
                    status.text = "Saved screenshot of " + title + ": " + path;
                    McpLog.Info(status.text);
                }
                else if (result is ErrorResponse error)
                {
                    status.text = error.Error;
                }
                else
                {
                    status.text = "Unexpected response while capturing the Editor window.";
                }
            }
            catch (Exception ex)
            {
                status.text = "Could not capture Editor window: " + ex.Message;
                McpLog.Error(status.text);
            }
            finally
            {
                refreshButton.SetEnabled(true);
                captureButton.SetEnabled(idsByChoice.ContainsKey(selector.value));
            }
        }

        private static async Task<object> ResolveCommandResultAsync(object result)
        {
            if (result is Task<object> pending)
            {
                return await pending;
            }

            return result;
        }

        private static string FormatSize(JToken value)
        {
            if (!(value is JObject dimensions)
                || dimensions["width"] == null || dimensions["height"] == null)
            {
                return "n/a";
            }

            int width = dimensions["width"].Value<int>();
            int height = dimensions["height"].Value<int>();
            return width + "×" + height;
        }

        private VisualElement CreateManageSceneActions()
        {
            var actions = new VisualElement();
            actions.AddToClassList("tool-item-actions");

            var gameViewButton = new Button(OnManageSceneScreenshotClicked)
            {
                text = "Game View"
            };
            gameViewButton.AddToClassList("tool-action-button");
            gameViewButton.style.marginTop = 4;
            gameViewButton.tooltip = "Capture a game camera screenshot. Default: Assets/Screenshots (configurable in Advanced).";

            var sceneViewButton = new Button(OnSceneViewScreenshotClicked)
            {
                text = "Scene View"
            };
            sceneViewButton.AddToClassList("tool-action-button");
            sceneViewButton.style.marginTop = 4;
            sceneViewButton.style.marginLeft = 4;
            sceneViewButton.tooltip = "Capture the active Scene View viewport. Default: Assets/Screenshots (configurable in Advanced).";

            var multiviewButton = new Button(OnManageSceneMultiviewClicked)
            {
                text = "Multiview"
            };
            multiviewButton.AddToClassList("tool-action-button");
            multiviewButton.style.marginTop = 4;
            multiviewButton.style.marginLeft = 4;
            multiviewButton.tooltip = "Capture a 6-angle contact sheet around the scene centre. Default: Assets/Screenshots (configurable in Advanced).";

            var captureLabel = new Label("Capture:");
            captureLabel.style.marginTop = 6;
            captureLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.Add(gameViewButton);
            row.Add(sceneViewButton);
            row.Add(multiviewButton);

            actions.Add(captureLabel);
            actions.Add(row);
            return actions;
        }

        private VisualElement CreateBatchExecuteSettings()
        {
            var container = new VisualElement();
            container.AddToClassList("tool-item-actions");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 4;

            var label = new Label("Max commands per batch:");
            label.style.marginRight = 8;
            label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            container.Add(label);

            int currentValue = EditorPrefs.GetInt(
                EditorPrefKeys.BatchExecuteMaxCommands,
                BatchExecute.DefaultMaxCommandsPerBatch
            );

            var field = new IntegerField
            {
                value = Math.Clamp(currentValue, 1, BatchExecute.AbsoluteMaxCommandsPerBatch),
                style = { width = 60 }
            };
            field.tooltip = $"Number of commands allowed per batch_execute call (1–{BatchExecute.AbsoluteMaxCommandsPerBatch}). Default: {BatchExecute.DefaultMaxCommandsPerBatch}.";

            field.RegisterValueChangedCallback(evt =>
            {
                int clamped = Math.Clamp(evt.newValue, 1, BatchExecute.AbsoluteMaxCommandsPerBatch);
                if (clamped != evt.newValue)
                {
                    field.SetValueWithoutNotify(clamped);
                }
                EditorPrefs.SetInt(EditorPrefKeys.BatchExecuteMaxCommands, clamped);
            });

            container.Add(field);

            var hint = new Label($"(max {BatchExecute.AbsoluteMaxCommandsPerBatch})");
            hint.style.marginLeft = 4;
            hint.style.color = new UnityEngine.Color(0.5f, 0.5f, 0.5f);
            hint.style.fontSize = 10;
            container.Add(hint);

            return container;
        }

        private void OnManageSceneScreenshotClicked()
        {
            try
            {
                var response = ManageScene.ExecuteScreenshot();
                if (response is SuccessResponse success && !string.IsNullOrWhiteSpace(success.Message))
                {
                    McpLog.Info(success.Message);
                }
                else if (response is ErrorResponse error && !string.IsNullOrWhiteSpace(error.Error))
                {
                    McpLog.Error(error.Error);
                }
                else
                {
                    McpLog.Info("Screenshot capture requested.");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to capture screenshot: {ex.Message}");
            }
        }

        private void OnSceneViewScreenshotClicked()
        {
            try
            {
                var response = ManageScene.ExecuteSceneViewScreenshot();
                if (response is SuccessResponse success && !string.IsNullOrWhiteSpace(success.Message))
                {
                    McpLog.Info(success.Message);
                }
                else if (response is ErrorResponse error && !string.IsNullOrWhiteSpace(error.Error))
                {
                    McpLog.Error(error.Error);
                }
                else
                {
                    McpLog.Info("Scene View screenshot capture requested.");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to capture Scene View screenshot: {ex.Message}");
            }
        }

        private void OnManageSceneMultiviewClicked()
        {
            try
            {
                var response = ManageScene.ExecuteMultiviewScreenshot();
                if (response is SuccessResponse success)
                {
                    // The data object is an anonymous type with imageBase64 — serialize to extract it
                    var json = Newtonsoft.Json.Linq.JObject.FromObject(success.Data);
                    string base64 = json["imageBase64"]?.ToString();
                    if (!string.IsNullOrEmpty(base64))
                    {
                        string folderSpec = MCPForUnity.Editor.Helpers.ScreenshotPreferences.Resolve(null);
                        string folder;
                        try
                        {
                            folder = MCPForUnity.Runtime.Helpers.ScreenshotUtility.ResolveFolderAbsolute(folderSpec);
                        }
                        catch (System.InvalidOperationException ex)
                        {
                            McpLog.Error(ex.Message);
                            return;
                        }
                        if (!System.IO.Directory.Exists(folder))
                            System.IO.Directory.CreateDirectory(folder);

                        string fileName = $"Multiview_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filePath = System.IO.Path.Combine(folder, fileName);
                        System.IO.File.WriteAllBytes(filePath, Convert.FromBase64String(base64));

                        // Import only when the file landed under Assets/.
                        string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")).Replace('\\', '/');
                        string normalizedFile = filePath.Replace('\\', '/');
                        string normalizedRoot = projectRoot.EndsWith("/") ? projectRoot : projectRoot + "/";
                        string projectRelative = normalizedFile.StartsWith(normalizedRoot, System.StringComparison.OrdinalIgnoreCase)
                            ? normalizedFile.Substring(normalizedRoot.Length)
                            : normalizedFile;
                        if (MCPForUnity.Runtime.Helpers.ScreenshotUtility.IsUnderAssets(projectRelative))
                            AssetDatabase.ImportAsset(projectRelative, ImportAssetOptions.ForceSynchronousImport);

                        McpLog.Info($"Multiview contact sheet saved to {projectRelative}");
                    }
                    else
                    {
                        McpLog.Info(success.Message ?? "Multiview capture completed.");
                    }
                }
                else if (response is ErrorResponse error && !string.IsNullOrWhiteSpace(error.Error))
                {
                    McpLog.Error(error.Error);
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to capture multiview: {ex.Message}");
            }
        }

        private static Label CreateTag(string text)
        {
            var tag = new Label(text);
            tag.AddToClassList("tool-tag");
            return tag;
        }

        private static bool IsManageSceneTool(ToolMetadata tool) => string.Equals(tool?.Name, "manage_scene", StringComparison.OrdinalIgnoreCase);

        private static bool IsManageCameraTool(ToolMetadata tool) => string.Equals(tool?.Name, "manage_camera", StringComparison.OrdinalIgnoreCase);

        private static bool IsBatchExecuteTool(ToolMetadata tool) => string.Equals(tool?.Name, "batch_execute", StringComparison.OrdinalIgnoreCase);

        private static bool IsBuiltIn(ToolMetadata tool) => tool?.IsBuiltIn ?? false;
    }
}
