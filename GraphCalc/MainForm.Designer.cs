using System;
using System.Drawing;

using System.Drawing.Drawing2D;


using System.Windows.Forms;

namespace GraphCalc;

public partial class MainForm
{
    private const int ButtonCornerRadius = 12;

    private Label OperationLabel = null!;
    private TextBox DisplayTextBox = null!;
    private TableLayoutPanel LayoutPanel = null!;
    private CheckBox ThemeToggleCheckBox = null!;
    private Label HistoryLabel = null!;
    private ListBox HistoryListBox = null!;

    private void InitializeComponent()
    {
        SuspendLayout();

        LayoutPanel = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = 11,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(245, 245, 245)
        };

        for (var i = 0; i < LayoutPanel.ColumnCount; i++)
        {
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 18F));
        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var i = 5; i < LayoutPanel.RowCount; i++)
        {
            LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10.333F));
        }

        ThemeToggleCheckBox = new CheckBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            Margin = new Padding(0, 0, 4, 4),
            Text = "Sötét mód",
            UseVisualStyleBackColor = false
        };
        ThemeToggleCheckBox.CheckedChanged += OnThemeToggleCheckedChanged;
        LayoutPanel.Controls.Add(ThemeToggleCheckBox, 0, 0);
        LayoutPanel.SetColumnSpan(ThemeToggleCheckBox, 4);

        var displayPanel = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        displayPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        displayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        OperationLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.BottomRight,
            Margin = new Padding(0),
            AutoSize = false
        };
        displayPanel.Controls.Add(OperationLabel, 0, 0);

        DisplayTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24F, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Margin = new Padding(0),
            TabStop = false
        };
        displayPanel.Controls.Add(DisplayTextBox, 0, 1);

        LayoutPanel.Controls.Add(displayPanel, 0, 1);
        LayoutPanel.SetColumnSpan(displayPanel, 4);

        HistoryLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Memória",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 4),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };
        LayoutPanel.Controls.Add(HistoryLabel, 0, 2);
        LayoutPanel.SetColumnSpan(HistoryLabel, 4);

        HistoryListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 8),
            IntegralHeight = false,
            SelectionMode = SelectionMode.One
        };
        HistoryListBox.SelectedIndexChanged += OnHistorySelectedIndexChanged;
        LayoutPanel.Controls.Add(HistoryListBox, 0, 3);
        LayoutPanel.SetColumnSpan(HistoryListBox, 4);

        var memoryButtonPanel = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8)
        };
        memoryButtonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        memoryButtonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        memoryButtonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        memoryButtonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        static void ConfigureMemoryButton(Button button)
        {
            button.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            button.Margin = new Padding(2);
        }

        var memoryAddButton = CreateButton("M+", OnMemoryStoreClick, "memory-add");
        ConfigureMemoryButton(memoryAddButton);
        memoryButtonPanel.Controls.Add(memoryAddButton, 0, 0);

        var memoryRemoveButton = CreateButton("M-", OnMemoryDeleteClick, "memory-remove");
        ConfigureMemoryButton(memoryRemoveButton);
        memoryButtonPanel.Controls.Add(memoryRemoveButton, 1, 0);

        var memoryClearButton = CreateButton("MC", OnMemoryClearClick, "memory-clear");
        ConfigureMemoryButton(memoryClearButton);
        memoryButtonPanel.Controls.Add(memoryClearButton, 2, 0);

        LayoutPanel.Controls.Add(memoryButtonPanel, 0, 4);
        LayoutPanel.SetColumnSpan(memoryButtonPanel, 4);

        AddButton("sin", OnUnaryOperationClick, 0, 5, tag: "sin");
        AddButton("cos", OnUnaryOperationClick, 1, 5, tag: "cos");
        AddButton("√", OnUnaryOperationClick, 2, 5, tag: "sqrt");
        AddButton("n!", OnUnaryOperationClick, 3, 5, tag: "fact");

        AddButton("CE", OnClearEntryClick, 0, 6);
        AddButton("C", OnClearAllClick, 1, 6);
        AddButton("⌫", OnBackspaceClick, 2, 6, tag: "Backspace");
        AddButton("÷", OnOperatorClick, 3, 6, tag: "/");

        AddButton("7", OnDigitClick, 0, 7);
        AddButton("8", OnDigitClick, 1, 7);
        AddButton("9", OnDigitClick, 2, 7);
        AddButton("×", OnOperatorClick, 3, 7, tag: "*");

        AddButton("4", OnDigitClick, 0, 8);
        AddButton("5", OnDigitClick, 1, 8);
        AddButton("6", OnDigitClick, 2, 8);
        AddButton("-", OnOperatorClick, 3, 8);

        AddButton("1", OnDigitClick, 0, 9);
        AddButton("2", OnDigitClick, 1, 9);
        AddButton("3", OnDigitClick, 2, 9);
        AddButton("+", OnOperatorClick, 3, 9);

        AddButton("±", OnToggleSignClick, 0, 10);
        AddButton("0", OnDigitClick, 1, 10);
        AddButton(",", OnDecimalClick, 2, 10);
        AddButton("=", OnEqualsClick, 3, 10);

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(320, 560);
        Controls.Add(LayoutPanel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Grafikus Számológép";

        ResumeLayout(false);

        void AddButton(string text, EventHandler handler, int column, int row, string? tag = null)
        {
            var button = CreateButton(text, handler, tag);
            LayoutPanel.Controls.Add(button, column, row);
        }
    }

    private Button CreateButton(string text, EventHandler handler, string? tag)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(4),
            Tag = tag ?? text,
            FlatStyle = FlatStyle.Flat
        };

        button.FlatAppearance.BorderSize = 0;
        button.Click += handler;
        button.Resize += OnButtonResize;
        ApplyRoundedCorners(button);

        ApplyButtonTheme(button);

        return button;
    }

    private void OnButtonResize(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            ApplyRoundedCorners(button);
        }
    }

    private void ApplyRoundedCorners(Button button)
    {
        if (button.Width <= 0 || button.Height <= 0)
        {
            return;
        }

        using var path = CreateRoundedRectanglePath(button.ClientRectangle, ButtonCornerRadius);
        button.Region?.Dispose();
        button.Region = new Region(path);
    }

    private GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}