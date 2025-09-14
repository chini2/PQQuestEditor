// PQQuest Save Editor – Antibombas (WinForms, .NET 6/8)
// - Pot e inventario separados: el pot se edita en visitCharacter.cookingPotList[0]
//   (recipeID/rarityID/rankID/cookTime). No se tocan ingredientes.
// - Pestañas: Pokémon / Inventario / Misc (tabs visibles con OwnerDraw).
// - Columna "Nombre" (reconstruida desde array de chars con '\0') + encabezados claros.
// - Antibombas: niveles/stats/slots/attach en characterStorage; pot solo valida rangos.
// - Layout: SplitContainer + sizing de columnas (FillWeight/MinimumWidth) + scroll.
// - FIX: Controls.Add(scMain) + RefreshSplitter blindado (sin SplitterDistance negativo).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

public class MainForm : Form
{
    // ---- Tipos para binding seguro ----
    class ComboItem { public int Id { get; set; } public string Label { get; set; } = ""; }

    // Flag para respetar movimiento manual del splitter
    bool _userMovedSplitter = false;

    // ---- UI superior (barra) ----
    Button btnOpen = new() { Text = "Abrir", AutoSize = true };
    Button btnSave = new() { Text = "Guardar", AutoSize = true };

    ComboBox cmbRecipe = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    ComboBox cmbRarity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    ComboBox cmbRank = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    Label lblCookingPreview = new() { AutoSize = true, Text = "(cooking: —)" };

    Button btnValidate = new() { Text = "VALIDAR (antibombas)", AutoSize = true };
    Button btnAutofix = new() { Text = "AUTO-ARREGLAR", AutoSize = true };

    Label lblFile = new() { AutoSize = true, Text = "(sin archivo)" };

    // ---- SplitContainer (tabs arriba / log abajo) ----
    SplitContainer scMain = new()
    {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Horizontal,
        FixedPanel = FixedPanel.Panel2,
        SplitterWidth = 6
    };

    // ---- Tabs ----
    TabControl tabs = new() { Dock = DockStyle.Fill, Alignment = TabAlignment.Top, SizeMode = TabSizeMode.Fixed };
    TabPage tabPokemon = new("Pokémon");
    TabPage tabInv = new("Inventario");
    TabPage tabMisc = new("Misc");

    // ---- Grids ----
    DataGridView gridPokemon = new()
    {
        Dock = DockStyle.Fill,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        ColumnHeadersVisible = true
    };

    DataGridView gridInventory = new()
    {
        Dock = DockStyle.Fill,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false
    };
    BindingSource invBinding = new();

    // ---- Misc Tab ----
    NumericUpDown numTickets = new() { Minimum = 0, Maximum = 9_999_999, Width = 120 };
    ComboBox cmbLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    // ---- Log ----
    TextBox txtLog = new() { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    // ---- Inventario VM ----
    class InvRow { public int Id { get; set; } public string Nombre { get; set; } = ""; public int Cantidad { get; set; } public bool Nuevo { get; set; } }

    // ---- Estado JSON ----
    JsonNode? root;
    string? currentPath;

    const int SAFE_MAX_SLOTS = 12;

    // ---- Catálogos ----
    readonly (int id, string nameEs)[] RECIPES = new (int, string)[] {
        (0,"Guiso de lo que sea (Mulligan)"),
        (1,"Estofado rojo"),(2,"Refresco azul"),(3,"Curry amarillo"),(4,"Avena gris"),
        (5,"Dip delicioso"),(6,"Sopa viscosa"),(7,"Pastel de barro"),(8,"Batido de verduras"),(9,"Néctar de miel"),
        (10,"Comida cerebral"),(11,"Sopa de piedras"),(12,"Cazuela ligera"),(13,"Olla caliente"),
        (14,"Crêpe sencillo"),(15,"Estofado fortificante"),(16,"Jarabe musculoso"),(17,"Ambrosía de leyendas")
    };
    readonly (int id, string label)[] RARITY = new (int, string)[] {
        (0,"Normal (1★)"),(1,"Buena (2★)"),(2,"Muy buena (3★)"),(3,"Perfecta/XXL (4★)")
    };
    readonly (int id, string label)[] RANK = new (int, string)[] {
        (0,"Normal Pot"),(1,"Bronze Pot"),(2,"Silver Pot"),(3,"Gold Pot")
    };
    readonly (int id, string name)[] LANG = new (int, string)[] {
        (0,"Japonés"),(1,"Inglés (US)"),(2,"Inglés"),(3,"Francés"),(4,"Alemán"),(5,"Alemán alt"),
        (6,"Español"),(7,"Italiano"),(8,"Coreano"),(9,"Chino Trad."),(10,"Chino Simpl.")
    };

    // Ingredientes
    readonly Dictionary<int, string> INGREDIENTS = new() {
        {1,"Tiny Mushroom"},{2,"Bluk Berry"},{3,"Apricorn"},{4,"Fossil"},
        {5,"Big Root"},{6,"Icy Rock"},{7,"Honey"},{8,"Balm Mushroom"},{9,"Rainbow Matter"}
    };

    public MainForm()
    {
        Text = "PQQuest Save Editor – Antibombas";
        Width = 1200; Height = 800;
        AutoScaleMode = AutoScaleMode.Dpi;
        this.WindowState = FormWindowState.Maximized; // opcional: abre maximizada

        // ---- Top bar ----
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
            WrapContents = true
        };
        top.Controls.AddRange(new Control[] {
            btnOpen, btnSave,
            new Label{Text="  Archivo:",AutoSize=true}, lblFile,
            new Label{Text="  Receta:",AutoSize=true}, cmbRecipe,
            new Label{Text="  Rareza:",AutoSize=true}, cmbRarity,
            new Label{Text="  Olla:",AutoSize=true},    cmbRank,
            new Label{Text="  ",AutoSize=true}, lblCookingPreview,
            btnValidate, btnAutofix
        });
       

        // ---- Tabs (forzamos altura visible y dibujo) ----
        // ---- Tabs (SIN OwnerDraw; visibles y grandes) ----
        tabs.Alignment = TabAlignment.Top;
        tabs.DrawMode = TabDrawMode.Normal;   // importante
        tabs.Appearance = TabAppearance.Normal;
        tabs.Multiline = false;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(160, 40);    // alto 40 px
        tabs.Padding = new Point(24, 12);


        tabs.TabPages.Add(tabPokemon);
        tabs.TabPages.Add(tabInv);
        tabs.TabPages.Add(tabMisc);

        // ====== Layout interno del Panel1: botonera (auto) + tabs (100%) ======
        var panel1Layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // fila 0: botonera
        panel1Layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));     // fila 1: TabControl

        // añade el layout al Panel1 del split
        scMain.Panel1.Controls.Add(panel1Layout);

        // botonera en fila 0
        panel1Layout.Controls.Add(tabs, 0, 0);

        // tabs en fila 1 (el TabControl ya debe tener Dock = Fill)
        panel1Layout.Controls.Add(tabs, 0, 1);



        // ---- SplitContainer ----
        // ====== Layout raíz del formulario: top (auto) + scMain (100%) ======
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));           // fila 0: barra principal
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));      // fila 1: SplitContainer

        // barra principal en fila 0
        rootLayout.Controls.Add(top, 0, 0);

        // SplitContainer en fila 1
        rootLayout.Controls.Add(scMain, 0, 1);
        Controls.Add(rootLayout);

        // panel2 = log (ya lo tenías)
        scMain.Panel2.Controls.Add(txtLog);
        // <<<< ¡IMPORTANTE! (antes de enganchar eventos)

        // Coloca el separador cuando la ventana ya tiene tamaño real
        this.Shown += (s, e) => RefreshSplitter();

        // Si el usuario mueve el separador, NO lo volvemos a forzar
        scMain.SplitterMoved += (s, e) => _userMovedSplitter = true;

        // Si cambia el tamaño de la ventana, re-ajusta SOLO si el usuario no lo movió
        this.Resize += (s, e) => { if (!_userMovedSplitter) RefreshSplitter(); };

        // ---- Pokémon grid ----
        gridPokemon.Columns.Clear();
        gridPokemon.ColumnHeadersHeight = 28;
        gridPokemon.EnableHeadersVisualStyles = false;
        gridPokemon.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        gridPokemon.ScrollBars = ScrollBars.Both; // H/V scroll

        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "displayName", HeaderText = "Nombre" });
        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "species", HeaderText = "Especie (No.)" });
        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "level", HeaderText = "Nivel" });
        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "hp", HeaderText = "HP base" });
        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "attack", HeaderText = "ATK base" });
        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "nature", HeaderText = "Naturaleza (ID)" });
        gridPokemon.Columns.Add(new DataGridViewTextBoxColumn { Name = "activeSlots", HeaderText = "Slots activos" });
        ConfigurePokemonGridSizing();
        tabPokemon.Controls.Add(gridPokemon);

        // ---- Inventario grid ----
        gridInventory.DataSource = invBinding;
        gridInventory.Columns.Clear();
        gridInventory.ScrollBars = ScrollBars.Both;
        gridInventory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", ReadOnly = true, Name = "Id" });
        gridInventory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Nombre", HeaderText = "Ingrediente", ReadOnly = true, Name = "Nombre" });
        gridInventory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cantidad", HeaderText = "Cantidad", Name = "Cantidad" });
        gridInventory.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Nuevo", HeaderText = "isNew", Name = "Nuevo" });
        ConfigureInventoryGridSizing();
        tabInv.Controls.Add(gridInventory);

        // ---- Misc tab ----
        var miscPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = false, ColumnCount = 2, Padding = new Padding(10) };
        miscPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        miscPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        miscPanel.Controls.Add(new Label { Text = "Idioma:", AutoSize = true }, 0, 0);
        miscPanel.Controls.Add(cmbLanguage, 1, 0);
        miscPanel.Controls.Add(new Label { Text = "Tickets:", AutoSize = true }, 0, 1);
        miscPanel.Controls.Add(numTickets, 1, 1);
        tabMisc.Controls.Add(miscPanel);

        // --- Pokémon grid: encabezado alto/auto ---
        gridPokemon.Margin = new Padding(0);
        gridPokemon.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridPokemon.ColumnHeadersHeight = Math.Max(gridPokemon.ColumnHeadersHeight, 40);
        gridPokemon.EnableHeadersVisualStyles = true;  // que el tema del SO maneje contraste

        // --- Inventario grid: encabezado alto/auto ---
        gridInventory.Margin = new Padding(0);
        gridInventory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridInventory.ColumnHeadersHeight = Math.Max(gridInventory.ColumnHeadersHeight, 40);
        gridInventory.EnableHeadersVisualStyles = true;




        // ---- Data sources (binding con ComboItem) ----
        cmbRecipe.DisplayMember = nameof(ComboItem.Label);
        cmbRecipe.ValueMember = nameof(ComboItem.Id);
        cmbRecipe.DataSource = RECIPES.Select(r => new ComboItem { Id = r.id, Label = $"{r.id} – {r.nameEs}" }).ToList();

        cmbRarity.DisplayMember = nameof(ComboItem.Label);
        cmbRarity.ValueMember = nameof(ComboItem.Id);
        cmbRarity.DataSource = RARITY.Select(r => new ComboItem { Id = r.id, Label = r.label }).ToList();

        cmbRank.DisplayMember = nameof(ComboItem.Label);
        cmbRank.ValueMember = nameof(ComboItem.Id);
        cmbRank.DataSource = RANK.Select(r => new ComboItem { Id = r.id, Label = r.label }).ToList();

        cmbLanguage.DisplayMember = nameof(ComboItem.Label);
        cmbLanguage.ValueMember = nameof(ComboItem.Id);
        cmbLanguage.DataSource = LANG.Select(l => new ComboItem { Id = l.id, Label = l.name }).ToList();

        // ---- Eventos ----
        btnOpen.Click += (s, e) => OpenJson();
        btnSave.Click += (s, e) => SaveJson();

        // Pot: SOLO edita recipeID/rarityID/rankID en cookingPotList[0]
        cmbRecipe.SelectedValueChanged += (s, e) => { SetCookingField("recipeID", Val(cmbRecipe.SelectedValue)); UpdateCookingPreview(); };
        cmbRarity.SelectedValueChanged += (s, e) => { SetCookingField("rarityID", Val(cmbRarity.SelectedValue)); UpdateCookingPreview(); };
        cmbRank.SelectedValueChanged += (s, e) => { SetCookingField("rankID", Val(cmbRank.SelectedValue)); UpdateCookingPreview(); };

        numTickets.ValueChanged += (s, e) => { if (root != null) root["tickets"] = (int)numTickets.Value; };
        cmbLanguage.SelectedValueChanged += (s, e) => { if (root != null) root["languageId"] = Val(cmbLanguage.SelectedValue); };

        gridInventory.CellEndEdit += (s, e) => ApplyInventoryToJson();
        gridPokemon.CellEndEdit += (s, e) => ApplyPokemonGridToJson();
        gridPokemon.UserDeletedRow += (s, e) => ApplyPokemonGridToJson();

        btnValidate.Click += (s, e) => RunValidation(autoFix: false);
        btnAutofix.Click += (s, e) => RunValidation(autoFix: true);
    }

    // ---------- Dibujo de tabs (OwnerDraw) ----------
    void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var tc = (TabControl)sender!;
        var tab = tc.TabPages[e.Index];
        var r = e.Bounds;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        using (var back = new SolidBrush(selected ? SystemColors.ControlLightLight : SystemColors.Control))
            e.Graphics.FillRectangle(back, r);

        TextRenderer.DrawText(
            e.Graphics,
            tab.Text,
            tc.Font,
            r,
            SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        );

        e.Graphics.DrawRectangle(SystemPens.ControlDark, r);
    }

    // ---------- Splitter helpers ----------
    void RefreshSplitter()
    {
        // Evita tocar mientras está minimizado o sin tamaño válido
        if (WindowState == FormWindowState.Minimized) return;
        if (!scMain.IsHandleCreated) return;
        if (scMain.Parent == null) return;          // aún no está agregado al form
        if (scMain.Height <= 0) return;

        int available = scMain.Height - scMain.SplitterWidth - scMain.Panel2MinSize;
        if (available <= 0)
        {
            scMain.Panel2MinSize = Math.Max(0, scMain.Height - scMain.SplitterWidth - 1);
            scMain.SplitterDistance = 0;
            return;
        }

        SetSplitterForTabs(ratio: 0.82, minPanel1: 420); // ~82% para tabs+grid; mínimo 420 px
    }

    void SetSplitterForTabs(double ratio, int minPanel1)
    {
        int available = scMain.Height - scMain.SplitterWidth - scMain.Panel2MinSize;
        if (available <= 0) { scMain.SplitterDistance = 1; return; }

        int target = (int)Math.Round(scMain.Height * ratio);
        int desired = Math.Max(minPanel1, target);

        // Clamp: [0 .. available]
        int clamped = Math.Max(0, Math.Min(desired, available));
        scMain.SplitterDistance = clamped;
    }

    // ---------- Archivo ----------
    void OpenJson()
    {
        using var ofd = new OpenFileDialog { Filter = "JSON (*.json)|*.json|Todos (*.*)|*.*" };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        currentPath = ofd.FileName; lblFile.Text = Path.GetFileName(currentPath);
        var txt = File.ReadAllText(currentPath);
        root = JsonNode.Parse(txt, new JsonNodeOptions { PropertyNameCaseInsensitive = true });

        // Tickets (nuevo/legacy)
        var misc = root?["SerializeData"]?["misc"] as JsonObject;
        int tickets = misc != null ? GetInt(misc, "fsGiftTicketNum", GetInt(root, "tickets", 0)) : GetInt(root, "tickets", 0);
        numTickets.Value = Math.Clamp(tickets, (int)numTickets.Minimum, (int)numTickets.Maximum);

        // Idioma
        SetComboByValue(cmbLanguage, GetInt(root, "languageId", 6));

        // Pot 0
        EnsureCookingPotDefaults();
        var pot0 = GetPot0();
        if (pot0 != null)
        {
            SetComboByValue(cmbRecipe, GetInt(pot0, "recipeID", 0));
            SetComboByValue(cmbRarity, GetInt(pot0, "rarityID", 0));
            SetComboByValue(cmbRank, GetInt(pot0, "rankID", 0));
        }
        UpdateCookingPreview();

        // Cargar grids
        LoadPokemonGridFromCharacterDictionary();
        LoadInventoryGrid();

        Log("Cargado OK\n");
    }

    void SaveJson()
    {
        if (root == null) { MessageBox.Show("Abre un JSON primero."); return; }
        ApplyPokemonGridToJson();
        ApplyInventoryToJson();

        // Persistir tickets
        var misc = root?["SerializeData"]?["misc"] as JsonObject;
        if (misc != null) misc["fsGiftTicketNum"] = (int)numTickets.Value; else root["tickets"] = (int)numTickets.Value;

        using var sfd = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = (currentPath == null ? "edited.json" : Path.GetFileNameWithoutExtension(currentPath) + ".edited.json")
        };
        if (sfd.ShowDialog() != DialogResult.OK) return;
        File.WriteAllText(sfd.FileName, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        MessageBox.Show("JSON guardado. Reempaqueta con PqSave para volver a 'user'.");
    }

    // ---------- Pokémon ----------
    void LoadPokemonGridFromCharacterDictionary()
    {
        gridPokemon.Rows.Clear();
        var dict = root?["SerializeData"]?["characterStorage"]?["characterDataDictionary"] as JsonObject; if (dict == null) return;

        foreach (var kv in dict.OrderBy(k => k.Key))
        {
            if (kv.Value is not JsonObject node) continue;
            var data = node["data"] as JsonObject; if (data == null) continue;

            string displayName = GetNameFromArray(data["name"] as JsonArray);
            int monsterNo = GetInt(data, "monsterNo", 0);
            int level = GetInt(data, "level", 1);
            int hp = GetInt(data, "hp", 0);
            int atk = GetInt(data, "attack", 0);
            int natureId = GetInt(data, "seikaku", 0);
            int activeSlots = GetInt(data["potential"] as JsonObject, "activeSlots", 0);

            gridPokemon.Rows.Add(displayName, monsterNo, level, hp, atk, natureId, activeSlots);
        }
    }

    void ApplyPokemonGridToJson()
    {
        if (root == null) return;
        var dict = root?["SerializeData"]?["characterStorage"]?["characterDataDictionary"] as JsonObject; if (dict == null) return;

        int i = 0;
        foreach (var kv in dict.OrderBy(k => k.Key).ToList())
        {
            if (i >= gridPokemon.Rows.Count - 1) break; // -1 por fila nueva
            var row = gridPokemon.Rows[i]; i++;
            if (kv.Value is not JsonObject node) continue;
            var data = node["data"] as JsonObject; if (data == null) continue;

            // No reescribimos "name" (solo mostramos).
            data["monsterNo"] = ToInt(row.Cells["species"].Value, GetInt(data, "monsterNo", 0));
            data["level"] = ToInt(row.Cells["level"].Value, GetInt(data, "level", 1));
            data["hp"] = ToInt(row.Cells["hp"].Value, GetInt(data, "hp", 0));
            data["attack"] = ToInt(row.Cells["attack"].Value, GetInt(data, "attack", 0));
            data["seikaku"] = ToInt(row.Cells["nature"].Value, GetInt(data, "seikaku", 0));

            var pot = data["potential"] as JsonObject;
            if (pot != null) { pot["activeSlots"] = ToInt(row.Cells["activeSlots"].Value, GetInt(pot, "activeSlots", 0)); }
        }
    }

    static string GetNameFromArray(JsonArray? arr)
    {
        if (arr == null) return "";
        var sb = new StringBuilder(arr.Count);
        foreach (var x in arr)
        {
            var s = x?.ToString() ?? "";
            if (string.IsNullOrEmpty(s)) continue;
            char c = s[0];
            if (c == '\0') break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------- Inventario ----------
    void LoadInventoryGrid()
    {
        var list = root?["SerializeData"]?["itemStorage"]?["datas"] as JsonArray;
        if (list == null) { invBinding.DataSource = null; return; }
        var rows = new List<InvRow>();
        foreach (var n in list.OfType<JsonObject>())
        {
            int id = GetInt(n, "id", 0);
            rows.Add(new InvRow
            {
                Id = id,
                Nombre = INGREDIENTS.TryGetValue(id, out var nm) ? nm : $"Desconocido ({id})",
                Cantidad = GetInt(n, "num", 0),
                Nuevo = string.Equals(n["isNew"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase)
            });
        }
        invBinding.DataSource = rows;
    }
    void ApplyInventoryToJson()
    {
        var list = root?["SerializeData"]?["itemStorage"]?["datas"] as JsonArray;
        if (list == null || invBinding.DataSource is not List<InvRow> rows) return;
        var map = rows.ToDictionary(r => r.Id, r => r);
        foreach (var n in list.OfType<JsonObject>())
        {
            int id = GetInt(n, "id", 0);
            if (map.TryGetValue(id, out var r))
            {
                n["num"] = r.Cantidad;
                n["isNew"] = r.Nuevo;
            }
        }
    }

    // ---------- Antibombas ----------
    void RunValidation(bool autoFix)
    {
        if (root == null) { MessageBox.Show("Abre un JSON primero"); return; }
        int issues = 0, fixes = 0;

        // 1) Validar Pokémon en characterStorage.characterDataDictionary
        var dict = root?["SerializeData"]?["characterStorage"]?["characterDataDictionary"] as JsonObject;
        if (dict != null)
        {
            var seen = new HashSet<long>();
            foreach (var kv in dict)
            {
                if (kv.Value is not JsonObject node) continue;
                var data = node["data"] as JsonObject; if (data == null) continue;

                long id = GetLong(node, "id", 0);
                if (id == 0 || !seen.Add(id)) { issues++; if (autoFix) { node["id"] = GenUniqueId(seen); fixes++; } }

                FixRange(data, "level", 1, 100, autoFix, ref issues, ref fixes);
                FixRange(data, "hp", 0, 9999, autoFix, ref issues, ref fixes);
                FixRange(data, "attack", 0, 9999, autoFix, ref issues, ref fixes);

                var pot = data["potential"] as JsonObject;
                if (pot != null)
                {
                    FixRange(pot, "activeSlots", 0, SAFE_MAX_SLOTS, autoFix, ref issues, ref fixes);
                    fixes += SanitizeAttachArray(pot, "attachStoneStorageID", GetPotentialCount(), autoFix, ref issues);
                    fixes += SanitizeAttachArray(pot, "attachSkillStoneStorageID", GetPotentialCount(), autoFix, ref issues);
                }
            }
        }

        // 2) Pot coherente (solo rangos; no tocamos ingredientes)
        EnsureCookingPotDefaults();
        var pot0 = GetPot0()!;
        FixRange(pot0, "recipeID", 0, 17, autoFix, ref issues, ref fixes);
        FixRange(pot0, "rarityID", 0, 3, autoFix, ref issues, ref fixes);
        FixRange(pot0, "rankID", 0, 3, autoFix, ref issues, ref fixes);

        UpdateCookingPreview();
        Log($"{(autoFix ? "Auto-arreglo" : "Validación")} => problemas={issues}, fixes={fixes}\n");
        MessageBox.Show($"{(autoFix ? "Auto-arreglo" : "Validación completada")}\nProblemas detectados: {issues}\nArreglos aplicados: {fixes}");
    }

    int SanitizeAttachArray(JsonObject pot, string key, int invCount, bool autoFix, ref int issues)
    {
        int applied = 0;
        var arr = pot[key] as JsonArray; if (arr == null) return applied;
        for (int i = 0; i < arr.Count; i++)
        {
            int v = ToInt(arr[i]?.ToString(), -1);
            bool bad = !(v == -1 || (v >= 0 && v < invCount));
            if (bad) { issues++; if (autoFix) { arr[i] = -1; applied++; } }
        }
        return applied;
    }

    int GetPotentialCount() =>
        (root?["SerializeData"]?["potentialStorage"]?["potentialDatas"] as JsonArray)?.Count ?? 0;

    // ---------- CookingPot helpers ----------
    void EnsureCookingPotDefaults()
    {
        if (root == null) return;
        var sd = root["SerializeData"] as JsonObject;
        if (sd == null) { sd = new JsonObject(); root["SerializeData"] = sd; }

        var vc = sd["visitCharacter"] as JsonObject;
        if (vc == null) { vc = new JsonObject(); sd["visitCharacter"] = vc; }

        var list = vc["cookingPotList"] as JsonArray;
        if (list == null) { list = new JsonArray(); vc["cookingPotList"] = list; }

        if (list.Count == 0)
        {
            list.Add(new JsonObject
            {
                ["state"] = 1,
                ["cookingProgress"] = 0,
                ["cookingOldProgress"] = 0,
                ["recipeID"] = 0,
                ["rarityID"] = 0,
                ["rankID"] = 0,
                ["cookTime"] = 6
            });
        }
    }

    JsonObject? GetPot0()
    {
        return root?["SerializeData"]?["visitCharacter"]?["cookingPotList"] is JsonArray arr
            ? arr.FirstOrDefault() as JsonObject
            : null;
    }

    void SetCookingField(string key, int value)
    {
        EnsureCookingPotDefaults();
        var pot0 = GetPot0(); if (pot0 == null) return;
        string target = key switch
        {
            "recipeID" or "recipeId" => "recipeID",
            "rarityID" or "rarityId" => "rarityID",
            "rankID" or "rankId" => "rankID",
            "cookTime" => "cookTime",
            _ => key
        };
        pot0[target] = value;
    }

    void UpdateCookingPreview()
    {
        var pot0 = GetPot0();
        int rcp = pot0 != null ? GetInt(pot0, "recipeID", 0) : Val(cmbRecipe.SelectedValue);
        int rar = pot0 != null ? GetInt(pot0, "rarityID", 0) : Val(cmbRarity.SelectedValue);
        int rk = pot0 != null ? GetInt(pot0, "rankID", 0) : Val(cmbRank.SelectedValue);
        try
        {
            var rcpName = RECIPES.First(x => x.id == rcp).nameEs;
            var rarName = RARITY.First(x => x.id == rar).label;
            var rkName = RANK.First(x => x.id == rk).label;
            lblCookingPreview.Text = $"Cocinando: {rcpName} | Rareza {rarName} | {rkName}";
        }
        catch { lblCookingPreview.Text = "(cooking: —)"; }
    }

    // ---------- Sizing helpers ----------
    void ConfigurePokemonGridSizing()
    {
        gridPokemon.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridPokemon.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        gridPokemon.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        gridPokemon.ScrollBars = ScrollBars.Both;

        SetSizing(gridPokemon.Columns["displayName"], 200f, 140);
        SetSizing(gridPokemon.Columns["species"], 110f, 80);
        SetSizing(gridPokemon.Columns["level"], 70f, 60);
        SetSizing(gridPokemon.Columns["hp"], 90f, 80);
        SetSizing(gridPokemon.Columns["attack"], 90f, 80);
        SetSizing(gridPokemon.Columns["nature"], 120f, 90);
        SetSizing(gridPokemon.Columns["activeSlots"], 110f, 90);
    }

    void ConfigureInventoryGridSizing()
    {
        gridInventory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridInventory.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        gridInventory.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        gridInventory.ScrollBars = ScrollBars.Both;

        SetSizing(gridInventory.Columns["Id"], 80f, 60);
        SetSizing(gridInventory.Columns["Nombre"], 200f, 140);
        SetSizing(gridInventory.Columns["Cantidad"], 110f, 90);
        SetSizing(gridInventory.Columns["Nuevo"], 90f, 70);
    }

    static void SetSizing(DataGridViewColumn? c, float fillWeight, int minWidth)
    {
        if (c == null) return;
        c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        c.FillWeight = fillWeight;
        c.MinimumWidth = minWidth;
    }

    // ---------- Helpers generales ----------
    static int Val(object? o) { if (o == null) return 0; int.TryParse(o.ToString(), out int v); return v; }
    static void SetComboByValue(ComboBox cmb, int value)
    {
        for (int i = 0; i < cmb.Items.Count; i++) { cmb.SelectedIndex = i; if (Convert.ToInt32(cmb.SelectedValue) == value) return; }
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
    }
    static int GetInt(JsonNode? node, string key, int defVal) { try { var v = node is JsonObject o ? o[key] : node; if (v == null) return defVal; if (v is JsonValue jv && jv.TryGetValue<int>(out var i)) return i; int.TryParse(v.ToString(), out var p); return p; } catch { return defVal; } }
    static long GetLong(JsonNode? node, string key, long defVal) { try { var v = node is JsonObject o ? o[key] : node; if (v == null) return defVal; if (v is JsonValue jv && jv.TryGetValue<long>(out var i)) return i; long.TryParse(v.ToString(), out var p); return p; } catch { return defVal; } }
    static long GenUniqueId(HashSet<long> seen) { long id; var rnd = new Random(); do { id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + rnd.Next(1, 999_999); } while (seen.Contains(id)); seen.Add(id); return id; }
    static void FixRange(JsonObject o, string key, int min, int max, bool autoFix, ref int issues, ref int fixes) { int v = GetInt(o, key, min); if (v < min || v > max) { issues++; if (autoFix) { o[key] = Math.Clamp(v, min, max); fixes++; } } }
    static int ToInt(object? v, int defVal) => v == null ? defVal : (int.TryParse(v.ToString(), out var i) ? i : defVal);
    void Log(string s) { txtLog.AppendText(s); }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
