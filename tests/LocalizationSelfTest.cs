using System.ComponentModel;
using System.Text;

namespace TH06NCTrainer;

internal static class LocalizationSelfTest
{
    internal static void Run(StringBuilder report)
    {
        using var form = new TrainerForm(false);
        int checkedTexts = 0;
        void Check(Control parent)
        {
            if (L.English && parent.Text.Any(c => c >= '\u3400' && c <= '\u9fff'))
                throw new InvalidOperationException("Chinese text leaked into English UI: " + parent.Text);
            checkedTexts++;
            foreach (Control child in parent.Controls) Check(child);
        }
        Check(form);
        form.PreparePreview(); Check(form);
        if (L.T("测试", "Test") != (L.English ? "Test" : "测试")) throw new InvalidOperationException("Wrong language selection");
        if (L.English && L.Error(new Win32Exception(5, "拒绝访问")).Contains("拒绝")) throw new InvalidOperationException("OS language leaked into errors");
        if (!L.English && !L.Error(new Win32Exception(5, "Access denied")).Contains("错误代码")) throw new InvalidOperationException("OS language leaked into errors");
        report.AppendLine($"PASS: {L.Language} standalone language, {checkedTexts} control texts in waiting/preview states, localized OS errors");
    }
}
