using System.Windows.Forms;

public sealed class ProcessingLogForm : Form
{
    private readonly TextBox _logTextBox;

    public ProcessingLogForm()
    {
        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false
        };

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 520);
        Controls.Add(_logTextBox);
        MinimumSize = new Size(700, 360);
        Name = "ProcessingLogForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Ход обработки";
    }

    public void AppendLogLine(string message)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLogLine), message);
            return;
        }

        _logTextBox.AppendText(message + Environment.NewLine);
    }
}
