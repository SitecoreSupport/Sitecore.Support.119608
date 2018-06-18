namespace Sitecore.Support.Shell.Applications.ContentEditor
{
  using Sitecore.Diagnostics;
  using Sitecore.Shell.Applications.Dialogs.RulesEditor;
  using Sitecore.Text;
  using System;
  public class Rules
  {
    /// <summary>
    /// Parses the source.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="source">The source.</param>
    internal static void ParseSource([NotNull] RulesEditorOptions options, [NotNull] string source)
    {
      Assert.ArgumentNotNull(options, "options");
      Assert.ArgumentNotNull(source, "source");

      if (source == string.Empty)
      {
        return;
      }

      if (source.ToLowerInvariant().Contains("rulespath") || source.ToLowerInvariant().Contains("hideactions"))
      {
        var parameters = new UrlString(source);

        options.RulesPath = parameters["rulespath"];

        var hideActions = parameters["hideactions"];
        if (!string.IsNullOrEmpty(hideActions))
        {
          options.HideActions = string.Compare(hideActions, "true", StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        return;
      }

      options.RulesPath = source;
    }
  }
}