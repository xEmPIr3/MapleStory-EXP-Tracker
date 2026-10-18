// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MapleExpTracker
{
    /// <summary>Small dialog for the character's level and the name shown in the header.</summary>
    static class CharacterDialog
    {
        public static bool Ask(ref int level, ref string name)
        {
            using (var form = new Form())
            using (var levelBox = new NumericUpDown())
            using (var nameBox = new TextBox())
            {
                form.Text = "Character";
                form.Icon = AppIcon.Create(32);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.TopMost = true;
                form.ShowInTaskbar = false;
                form.AutoScaleMode = AutoScaleMode.None;
                form.Font = new Font("Segoe UI", 9f);
                form.ClientSize = new Size(320, 150);

                form.Controls.Add(new Label { Text = "Current level:", Location = new Point(16, 20), AutoSize = true });
                levelBox.Location = new Point(120, 17);
                levelBox.Width = 80;
                levelBox.Minimum = 1;
                levelBox.Maximum = 300;
                levelBox.Value = Math.Max(1, Math.Min(300, level));
                form.Controls.Add(levelBox);

                form.Controls.Add(new Label { Text = "Name (optional):", Location = new Point(16, 56), AutoSize = true });
                nameBox.Location = new Point(120, 53);
                nameBox.Width = 180;
                nameBox.MaxLength = 24;
                nameBox.Text = name ?? "";
                form.Controls.Add(nameBox);

                form.Controls.Add(new Label
                {
                    Text = "Levels 1-99 are detected from your EXP automatically.",
                    Location = new Point(16, 86),
                    AutoSize = true,
                    ForeColor = SystemColors.GrayText
                });

                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(144, 112), Size = new Size(76, 28) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(226, 112), Size = new Size(76, 28) };
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.Shown += delegate
                {
                    form.Activate();
                    levelBox.Select(0, levelBox.Text.Length);
                    levelBox.Focus();
                };

                if (form.ShowDialog() != DialogResult.OK) return false;
                level = (int)levelBox.Value;
                name = nameBox.Text.Trim();
                return true;
            }
        }
    }
}
