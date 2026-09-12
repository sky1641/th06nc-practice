using System.ComponentModel;
using System.Globalization;

namespace TH06NCTrainer;

internal static class L
{
#if ENGLISH
    internal const bool English = true;
#else
    internal const bool English = false;
#endif
    internal static string Language => English ? "en" : "zh-CN";
    internal static string T(string chinese, string english) => English ? english : chinese;
    internal static void Initialize()
    {
        var culture = CultureInfo.GetCultureInfo(English ? "en-US" : "zh-CN");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
    internal static string Error(Exception error)
    {
        // Windows system messages can use the OS language, regardless of app culture.
        // Keep raw diagnostics in error.log; localize the user-facing wrapper.
        if (error is Win32Exception native)
            return T($"系统调用失败，错误代码 {native.NativeErrorCode}。详情见 error.log。", $"Windows call failed (code {native.NativeErrorCode}). See error.log.");
        if (error is AggregateException aggregate)
            return string.Join("; ", aggregate.InnerExceptions.Select(Error));
        if (error is ArgumentException)
            return T("设置值超出允许范围或参数无效。", "A setting is out of range or invalid.");
        bool hasChinese = error.Message.Any(c => c >= '\u3400' && c <= '\u9fff');
        if (hasChinese == !English) return error.Message;
        return T("操作失败，详情见 error.log。", "Operation failed. See error.log for details.");
    }
}
