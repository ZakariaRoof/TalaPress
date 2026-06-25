using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using TalaPress.Pages;

namespace TalaPress.ViewComponents
{
    public class DynamicFormViewComponent : ViewComponent
    {
        private readonly IConfiguration _configuration;

        public DynamicFormViewComponent(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IViewComponentResult> InvokeAsync(long formId)
        {
            var model = new DynamicFormViewModel();

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return View(model);

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using (var cmd = new SqlCommand("SELECT Id, Name, Name_En, Description, Description_En, SubmitButtonText, SubmitButtonText_En, SuccessMessage, SuccessMessage_En, IsActive FROM dbo.Forms WHERE Id = @Id", connection))
            {
                cmd.Parameters.AddWithValue("@Id", formId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.Form = new FormViewModel
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Description_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                        SubmitButtonText = reader.IsDBNull(5) ? null : reader.GetString(5),
                        SubmitButtonText_En = reader.IsDBNull(6) ? null : reader.GetString(6),
                        SuccessMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                        SuccessMessage_En = reader.IsDBNull(8) ? null : reader.GetString(8),
                        IsActive = reader.GetBoolean(9)
                    };
                }
            }

            if (model.Form != null && model.Form.IsActive)
            {
                using var cmd = new SqlCommand("SELECT FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En, HelpText, HelpText_En, IsRequired, DefaultValue, OptionsJson FROM dbo.FormFields WHERE FormId = @FormId ORDER BY SortOrder", connection);
                cmd.Parameters.AddWithValue("@FormId", formId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var field = new FormFieldDto
                    {
                        FieldName = reader.GetString(0),
                        Label = reader.GetString(1),
                        Label_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        FieldType = reader.GetString(3),
                        Placeholder = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Placeholder_En = reader.IsDBNull(5) ? null : reader.GetString(5),
                        HelpText = reader.IsDBNull(6) ? null : reader.GetString(6),
                        HelpText_En = reader.IsDBNull(7) ? null : reader.GetString(7),
                        IsRequired = reader.GetBoolean(8),
                        DefaultValue = reader.IsDBNull(9) ? null : reader.GetString(9),
                        OptionsJson = reader.IsDBNull(10) ? null : reader.GetString(10)
                    };
                    
                    // If OptionsJson is present, maybe parse dynamic options
                    if (!string.IsNullOrEmpty(field.OptionsJson) && (field.FieldType == "Select" || field.FieldType == "Radio" || field.FieldType == "Checkbox"))
                    {
                        field.DynamicOptionsList = await ResolveDynamicOptionsAsync(connection, field.OptionsJson);
                    }
                    
                    model.Fields.Add(field);
                }
            }

            return View(model);
        }

        private async Task<List<SelectOption>> ResolveDynamicOptionsAsync(SqlConnection connection, string optionsJson)
        {
            var options = new List<SelectOption>();
            try
            {
                var doc = JsonDocument.Parse(optionsJson);
                var root = doc.RootElement;
                bool isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

                // Legacy fallback support for older forms
                if (root.TryGetProperty("sourceType", out var sourceType))
                {
                    string sType = sourceType.GetString() ?? "static";
                    if (sType == "static" && root.TryGetProperty("staticOptions", out var staticOptsTxt))
                    {
                        string[] parts = staticOptsTxt.GetString()?.Split(',') ?? Array.Empty<string>();
                        foreach (var part in parts)
                        {
                            var v = part.Trim();
                            if (!string.IsNullOrEmpty(v)) options.Add(new SelectOption { Value = v, Text = v });
                        }
                        return options;
                    }
                }

                if (root.TryGetProperty("mode", out var modeProp))
                {
                    string mode = modeProp.GetString() ?? "static";

                    if (mode == "static" && root.TryGetProperty("staticOptions", out var staticOpts) && staticOpts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var opt in staticOpts.EnumerateArray())
                        {
                            string value = opt.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                            string labelAr = opt.TryGetProperty("label", out var l) ? l.GetString() ?? value : value;
                            string labelEn = opt.TryGetProperty("labelEn", out var le) ? le.GetString() ?? labelAr : labelAr;
                            
                            options.Add(new SelectOption { Value = value, Text = isArabic ? labelAr : labelEn });
                        }
                    }
                    else if (mode == "query" && root.TryGetProperty("queryMode", out var qMode))
                    {
                        string queryMode = qMode.GetString() ?? "sql";
                        string finalSql = "";

                        if (queryMode == "sql" && root.TryGetProperty("sqlQuery", out var rawSql))
                        {
                            finalSql = rawSql.GetString() ?? "";
                        }
                        else if (queryMode == "builder" && root.TryGetProperty("query", out var queryObj))
                        {
                            string tableName = queryObj.TryGetProperty("tableName", out var tbl) ? tbl.GetString() ?? "" : "";
                            string logic = queryObj.TryGetProperty("logic", out var log) ? log.GetString() ?? "AND" : "AND";
                            
                            if (!string.IsNullOrEmpty(tableName) && tableName.All(c => char.IsLetterOrDigit(c) || c == '_'))
                            {
                                var whereClauses = new List<string>();
                                if (queryObj.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var rule in rules.EnumerateArray())
                                    {
                                        string field = rule.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "";
                                        string op = rule.TryGetProperty("operator", out var o) ? o.GetString() ?? "" : "";
                                        string val = rule.TryGetProperty("value", out var vl) ? vl.GetString() ?? "" : "";
                                        
                                        if (!string.IsNullOrEmpty(field) && field.All(c => char.IsLetterOrDigit(c) || c == '_'))
                                        {
                                            string escapedVal = val.Replace("'", "''");
                                            switch (op)
                                            {
                                                case "Equals": whereClauses.Add($"[{field}] = N'{escapedVal}'"); break;
                                                case "DoesNotEqual": whereClauses.Add($"[{field}] <> N'{escapedVal}'"); break;
                                                case "Contains": whereClauses.Add($"[{field}] LIKE N'%{escapedVal}%'"); break;
                                                case "StartsWith": whereClauses.Add($"[{field}] LIKE N'{escapedVal}%'"); break;
                                                case "EndsWith": whereClauses.Add($"[{field}] LIKE N'%{escapedVal}'"); break;
                                                case "GreaterThan": whereClauses.Add($"[{field}] > N'{escapedVal}'"); break;
                                                case "LessThan": whereClauses.Add($"[{field}] < N'{escapedVal}'"); break;
                                                case "IsBlank": whereClauses.Add($"([{field}] IS NULL OR [{field}] = '')"); break;
                                                case "IsNotBlank": whereClauses.Add($"([{field}] IS NOT NULL AND [{field}] <> '')"); break;
                                                case "In": 
                                                    var inValues = escapedVal.Split(',').Select(v => $"N'{v.Trim()}'");
                                                    whereClauses.Add($"[{field}] IN ({string.Join(",", inValues)})"); 
                                                    break;
                                            }
                                        }
                                    }
                                }
                                
                                string whereString = whereClauses.Any() ? " WHERE " + string.Join($" {logic} ", whereClauses) : "";
                                finalSql = $"SELECT * FROM dbo.[{tableName}]{whereString}";
                            }
                        }

                        if (!string.IsNullOrEmpty(finalSql) && finalSql.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !finalSql.Contains("DROP") && !finalSql.Contains("DELETE") && !finalSql.Contains("UPDATE") && !finalSql.Contains("INSERT"))
                        {
                            using var cmd = new SqlCommand(finalSql, connection);
                            using var reader = await cmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                if (queryMode == "sql")
                                {
                                    string val = reader.GetValue(0)?.ToString() ?? "";
                                    string textAr = reader.FieldCount > 1 ? (reader.GetValue(1)?.ToString() ?? val) : val;
                                    string textEn = reader.FieldCount > 2 ? (reader.GetValue(2)?.ToString() ?? textAr) : textAr;
                                    options.Add(new SelectOption { Value = val, Text = isArabic ? textAr : textEn });
                                }
                                else
                                {
                                    string val = "", textAr = "", textEn = "";
                                    bool foundId = false, foundName = false, foundNameEn = false;
                                    
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        string colName = reader.GetName(i);
                                        if (colName.Equals("Id", StringComparison.OrdinalIgnoreCase) && !foundId) { val = reader.GetValue(i)?.ToString() ?? ""; foundId = true; }
                                        else if (colName.Equals("Name", StringComparison.OrdinalIgnoreCase) && !foundName) { textAr = reader.GetValue(i)?.ToString() ?? ""; foundName = true; }
                                        else if (colName.Equals("Name_En", StringComparison.OrdinalIgnoreCase) && !foundNameEn) { textEn = reader.GetValue(i)?.ToString() ?? ""; foundNameEn = true; }
                                    }
                                    
                                    if (!foundId && reader.FieldCount > 0) val = reader.GetValue(0)?.ToString() ?? "";
                                    if (!foundName && reader.FieldCount > 1) textAr = reader.GetValue(1)?.ToString() ?? val;
                                    if (!foundNameEn) textEn = textAr;

                                    options.Add(new SelectOption { Value = val, Text = isArabic ? textAr : textEn });
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return options;
        }
    }

    public class DynamicFormViewModel
    {
        public FormViewModel? Form { get; set; }
        public List<FormFieldDto> Fields { get; set; } = new();
    }

    public class SelectOption
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}

namespace TalaPress.Pages
{
    // Extending FormFieldDto from FormsModel to hold the options list
    public partial class FormFieldDto
    {
        public List<TalaPress.ViewComponents.SelectOption> DynamicOptionsList { get; set; } = new();
    }
}
