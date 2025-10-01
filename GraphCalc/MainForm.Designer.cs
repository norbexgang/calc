using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GraphCalc;

public partial class MainForm
{
    private const int ButtonCornerRadius = 12;

    private TextBox DisplayTextBox = null!;
    private TableLayoutPanel LayoutPanel = null!;

    private void InitializeComponent()
    {
        SuspendLayout();

        LayoutPanel = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = 7,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(245, 245, 245)
        };

        for (var i = 0; i < LayoutPanel.ColumnCount; i++)
        {
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 22F));
        for (var i = 1; i < LayoutPanel.RowCount; i++)
        {
            LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 13F));
        }

        DisplayTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24F, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 8),
            TabStop = false
        };
        LayoutPanel.Controls.Add(DisplayTextBox, 0, 0);
        LayoutPanel.SetColumnSpan(DisplayTextBox, 4);

        AddButton("sin", OnUnaryOperationClick, 0, 1, tag: "sin");
        AddButton("cos", OnUnaryOperationClick, 1, 1, tag: "cos");
        AddButton("√", OnUnaryOperationClick, 2, 1, tag: "sqrt");
        AddButton("n!", OnUnaryOperationClick, 3, 1, tag: "fact");

        AddButton("CE", OnClearEntryClick, 0, 2);
        AddButton("C", OnClearAllClick, 1, 2);
        AddButton("⌫", OnBackspaceClick, 2, 2, tag: "Backspace");
        AddButton("÷", OnOperatorClick, 3, 2, tag: "/");

        AddButton("7", OnDigitClick, 0, 3);
        AddButton("8", OnDigitClick, 1, 3);
        AddButton("9", OnDigitClick, 2, 3);
        AddButton("×", OnOperatorClick, 3, 3, tag: "*");

        AddButton("4", OnDigitClick, 0, 4);
        AddButton("5", OnDigitClick, 1, 4);
        AddButton("6", OnDigitClick, 2, 4);
        AddButton("-", OnOperatorClick, 3, 4);

        AddButton("1", OnDigitClick, 0, 5);
        AddButton("2", OnDigitClick, 1, 5);
        AddButton("3", OnDigitClick, 2, 5);
        AddButton("+", OnOperatorClick, 3, 5);

        AddButton("±", OnToggleSignClick, 0, 6);
        AddButton("0", OnDigitClick, 1, 6);
        AddButton(",", OnDecimalClick, 2, 6);
        AddButton("=", OnEqualsClick, 3, 6);

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(320, 420);
        Controls.Add(LayoutPanel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Grafikus Számológép";

        ResumeLayout(false);
    }

    private void AddButton(string text, EventHandler handler, int column, int row, string? tag = null)
    {
        var button = CreateButton(text, handler, tag);
        LayoutPanel.Controls.Add(button, column, row);
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
            BackColor = Color.WhiteSmoke,
            FlatStyle = FlatStyle.Flat
        };

        button.FlatAppearance.BorderColor = Color.LightGray;
        button.FlatAppearance.BorderSize = 0;
        button.Click += handler;
        button.Resize += OnButtonResize;
        ApplyRoundedCorners(button);

        if (text is "÷" or "×" or "-" or "+")
        {
            button.BackColor = Color.FromArgb(225, 230, 246);
        }
        else if (text is "CE" or "C" or "⌫")
        {
            button.BackColor = Color.FromArgb(255, 236, 179);
        }
        else if (text is "sin" or "cos" or "√" or "n!")
        {
            button.BackColor = Color.FromArgb(232, 245, 233);
        }
        else if (text == "=")
        {
            button.BackColor = Color.FromArgb(76, 175, 80);
            button.ForeColor = Color.White;
        }

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

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
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
