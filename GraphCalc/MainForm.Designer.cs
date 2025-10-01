using System;
using System.Drawing;
using System.Windows.Forms;

namespace GraphCalc;

public partial class MainForm
{
    private TextBox DisplayTextBox = null!;
    private TableLayoutPanel LayoutPanel = null!;

    private void InitializeComponent()
    {
        SuspendLayout();

        LayoutPanel = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = 6,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(245, 245, 245)
        };

        for (var i = 0; i < LayoutPanel.ColumnCount; i++)
        {
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        for (var i = 1; i < LayoutPanel.RowCount; i++)
        {
            LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
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

        AddButton("CE", OnClearEntryClick, 0, 1);
        AddButton("C", OnClearAllClick, 1, 1);
        AddButton("⌫", OnBackspaceClick, 2, 1, tag: "Backspace");
        AddButton("÷", OnOperatorClick, 3, 1, tag: "/");

        AddButton("7", OnDigitClick, 0, 2);
        AddButton("8", OnDigitClick, 1, 2);
        AddButton("9", OnDigitClick, 2, 2);
        AddButton("×", OnOperatorClick, 3, 2, tag: "*");

        AddButton("4", OnDigitClick, 0, 3);
        AddButton("5", OnDigitClick, 1, 3);
        AddButton("6", OnDigitClick, 2, 3);
        AddButton("-", OnOperatorClick, 3, 3);

        AddButton("1", OnDigitClick, 0, 4);
        AddButton("2", OnDigitClick, 1, 4);
        AddButton("3", OnDigitClick, 2, 4);
        AddButton("+", OnOperatorClick, 3, 4);

        AddButton("±", OnToggleSignClick, 0, 5);
        AddButton("0", OnDigitClick, 1, 5);
        AddButton(",", OnDecimalClick, 2, 5);
        AddButton("=", OnEqualsClick, 3, 5);

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
        button.Click += handler;

        if (text is "÷" or "×" or "-" or "+")
        {
            button.BackColor = Color.FromArgb(225, 230, 246);
        }
        else if (text is "CE" or "C" or "⌫")
        {
            button.BackColor = Color.FromArgb(255, 236, 179);
        }
        else if (text == "=")
        {
            button.BackColor = Color.FromArgb(76, 175, 80);
            button.ForeColor = Color.White;
        }

        return button;
    }
}
