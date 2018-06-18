namespace Sitecore.Support.Data.Fields
{
  using System.Collections.Generic;
  using System.Linq;
  using System;
  using Sitecore.Configuration;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Links;
  using Sitecore.Text;
  using Sitecore.Web.UI.HtmlControls.Data;
  using Sitecore.Data.Fields;
  using Sitecore.Data;

  /// <summary>
  /// Represents a Link field.
  /// </summary>
  public class TemplateFieldSourceField : InternalLinkField
  {

    #region Fields

    /// <summary>
    /// Collection of keyword to be ignored
    /// </summary>
    private static readonly List<string> KeysToIgnore = new List<string> { "bindmode", "editor", "syntax" };

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new <see cref="LinkField"/> instance.
    /// </summary>
    /// <param name="innerField">Inner field.</param>
    /// <contract>
    ///   <requires name="innerField" condition="none" />
    /// </contract>
    public TemplateFieldSourceField([NotNull] Field innerField) : base(innerField)
    {
      Assert.ArgumentNotNull(innerField, "innerField");
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Converts a <see cref="Field"/> to a <see cref="LinkField"/>.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The implicit operator.</returns>
    public static implicit operator TemplateFieldSourceField([CanBeNull] Field field)
    {
      if (field != null)
      {
        return new TemplateFieldSourceField(field);
      }

      return null;
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Validates the links.
    /// </summary>
    /// <param name="result">The result.</param>
    public override void ValidateLinks([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string value = Value;
      value = FilterTargetPath(value);
      if (string.IsNullOrEmpty(value))
      {
        return;
      }

      if (!(InnerField.Item.TemplateID == TemplateIDs.TemplateField))
      {
        if (value.StartsWith("query:", StringComparison.InvariantCultureIgnoreCase))
        {
          return;
        }

        if (LookupSources.IsComplex(value))
        {
          ValidateComplexLookupSource(result, value);
          return;
        }

        return;
      }

      string type = InnerField.Item["Type"].ToLowerInvariant();

      switch (type)
      {
        case "lookup":
        case "valuelookup":
        case "droplist":
        case "checklist":
        case "multilist":
        case "grouped droplist":
          if (value.StartsWith("query:", StringComparison.InvariantCultureIgnoreCase))
          {
            return;
          }

          if (LookupSources.IsComplex(value))
          {
            ValidateComplexLookupSource(result, value);
            return;
          }

          break;
        case "iframe":
        case "icon":
          return;
      }

      if (type == "rich text" || type == "html")
      {
        ValidateRichText(result);
        return;
      }

      if ((type == "lookup") || (type == "droplink"))
      {
        if ((value.IndexOf('|') >= 0) || (value.ToLowerInvariant().Contains("query:")))
        {
          ValidateLookup(result);
          return;
        }
      }

      if (type == "tree" || type == "reference")
      {
        ValidateTree(result);
        return;
      }

      if (type == "tree list" || type == "treelist" || type == "treelistex")
      {
        ValidateTreeList(result);
        return;
      }

      if (type == "image")
      {
        ValidateImage(result);
        return;
      }

      if (type == "rules")
      {
        ValidateRules(result);
        return;
      }

      if (type == "custom")
      {
        ValidateCustomField(result);
        return;
      }

      base.ValidateLinks(result);
    }

    /// <summary>
    /// Check targetPath if it contains reserved value to be ignored, and perform filtering.
    /// </summary>
    /// <param name="targetPath"></param>
    /// <returns></returns>
    private string FilterTargetPath(string targetPath)
    {
      string filteredTargetPath = string.Empty;

      var paths = targetPath.Split('&');

      foreach (var path in paths)
      {
        var key = path.Split('=').First();

        if (!KeysToIgnore.Contains(key, StringComparer.InvariantCultureIgnoreCase))
        {
          filteredTargetPath += string.Join("&", path);
        }
      }

      return filteredTargetPath;
    }

    #endregion

    #region Private methods

    /// <summary>
    /// Validates the custom field.
    /// </summary>
    /// <param name="result">The result.</param>
    private void ValidateCustomField([NotNull]LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string source = Value;
      if (string.IsNullOrEmpty(source))
      {
        return;
      }

      source = source.Trim();

      if (string.IsNullOrEmpty(source))
      {
        return;
      }

      var parameters = new UrlString(source);

      string typeName = parameters["type"];
      if (string.IsNullOrEmpty(typeName))
      {
        return;
      }

      try
      {
        var customField = Sitecore.Reflection.ReflectionUtil.CreateObject(typeName) as Sitecore.Shell.Applications.ContentEditor.ICustomField;
        if (customField == null)
        {
          result.AddBrokenLink(source);
        }
      }
      catch
      {
        result.AddBrokenLink(source);
      }
    }

    /// <summary>
    /// Validates the source for the Rules field type.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    private void ValidateRules([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string value = Value;
      if (string.IsNullOrEmpty(value))
      {
        return;
      }

      var options = new Sitecore.Shell.Applications.Dialogs.RulesEditor.RulesEditorOptions();
      Sitecore.Support.Shell.Applications.ContentEditor.Rules.ParseSource(options, value);

      if (string.IsNullOrEmpty(options.RulesPath) || options.RulesPath.StartsWith("query:", StringComparison.InvariantCultureIgnoreCase))
      {
        return;
      }

      Database database = this.Database;
      Assert.IsNotNull(database, "content database");

      Item targetItem = database.GetItem(options.RulesPath);

      if (targetItem != null)
      {
        result.AddValidLink(targetItem, value);
      }
      else
      {
        result.AddBrokenLink(value);
      }
    }

    /// <summary>
    /// Validates the lookup.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    void ValidateLookup([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string value = Value;

      string[] parts = value.Split('|');

      foreach (string part in parts)
      {
        if (string.IsNullOrEmpty(part))
        {
          continue;
        }

        if (part.ToLowerInvariant().StartsWith("query:", StringComparison.InvariantCulture))
        {
          continue;
        }

        Item targetItem = InnerField.Database.GetItem(part);

        if (targetItem != null)
        {
          result.AddValidLink(targetItem, part);
        }
        else
        {
          result.AddBrokenLink(part);
        }
      }
    }

    /// <summary>
    /// Validates the rich text.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    void ValidateRichText([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string value = Value;
      if (string.IsNullOrEmpty(value))
      {
        return;
      }

      Database database = Factory.GetDatabase(Constants.CoreDatabaseName);
      Assert.IsNotNull(database, Constants.CoreDatabaseName);

      Item targetItem = database.GetItem(value);

      if (targetItem != null)
      {
        result.AddValidLink(targetItem, value);
      }
      else
      {
        result.AddBrokenLink(value);
      }
    }

    /// <summary>
    /// Validates the tree list.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    void ValidateTreeList([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      UrlString parameters = new UrlString(Value);

      string datasource = parameters["datasource"];
      if (string.IsNullOrEmpty(datasource))
      {
        return;
      }

      string databaseName = StringUtil.GetString(parameters["databasename"], InnerField.Database.Name);

      Database database = Factory.GetDatabase(databaseName);
      Assert.IsNotNull(database, databaseName);

      Item targetItem = database.GetItem(datasource);

      if (targetItem != null)
      {
        result.AddValidLink(targetItem, datasource);
      }
      else
      {
        result.AddBrokenLink(datasource);
      }
    }

    /// <summary>
    /// Validates the tree.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    void ValidateTree([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string source = Value;
      if (source.IndexOf("datasource=", StringComparison.InvariantCultureIgnoreCase) < 0)
      {
        base.ValidateLinks(result);
        return;
      }

      UrlString parameters = new UrlString(source);

      string datasource = parameters["datasource"];
      if (string.IsNullOrEmpty(datasource))
      {
        return;
      }

      string databaseName = StringUtil.GetString(parameters["databasename"], InnerField.Database.Name);

      Database database = Factory.GetDatabase(databaseName);
      Assert.IsNotNull(database, databaseName);

      Item targetItem = database.GetItem(datasource);

      if (targetItem != null)
      {
        result.AddValidLink(targetItem, datasource);
      }
      else
      {
        result.AddBrokenLink(datasource);
      }
    }

    /// <summary>
    /// Validates the complex source.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="value">The Value</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    void ValidateComplexLookupSource([NotNull] LinksValidationResult result, string value)
    {
      Assert.ArgumentNotNull(result, "result");

      Database database = LookupSources.GetDatabase(value);

      if (database == null)
      {
        database = InnerField.Database;
      }

      UrlString parameters = new UrlString(value);

      string datasource = parameters["datasource"];
      if (string.IsNullOrEmpty(datasource))
      {
        result.AddBrokenLink(value);
        return;
      }

      Item targetItem = database.GetItem(datasource);

      if (targetItem != null)
      {
        result.AddValidLink(targetItem, value);
      }
      else
      {
        result.AddBrokenLink(value);
      }
    }

    /// <summary>
    /// Validates the links.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <contract>
    ///   <requires name="result" condition="not null" />
    /// </contract>
    public void ValidateImage([NotNull] LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      string path = Path;
      if (string.IsNullOrEmpty(path))
      {
        return;
      }

      if (path.StartsWith("~", StringComparison.InvariantCulture))
      {
        path = StringUtil.Mid(path, 1);
      }

      Database database = Database;
      if (database == null)
      {
        return;
      }

      Item targetItem = database.GetItem(path);

      if (targetItem != null)
      {
        result.AddValidLink(targetItem, path);
      }
      else
      {
        result.AddBrokenLink(Path);
      }
    }

    #endregion
  }
}