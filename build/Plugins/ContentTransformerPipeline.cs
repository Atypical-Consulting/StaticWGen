using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Serilog.Log;

/// <summary>
/// Discovers and runs content transformers in order.
/// Scans the current assembly for classes implementing IContentTransformer.
/// </summary>
public static class ContentTransformerPipeline
{
    private static List<IContentTransformer>? _transformers;

    public static List<IContentTransformer> Transformers
    {
        get
        {
            if (_transformers != null)
                return _transformers;

            _transformers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IContentTransformer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => (IContentTransformer)Activator.CreateInstance(t)!)
                .OrderBy(t => t.Order)
                .ToList();

            if (_transformers.Count > 0)
            {
                Information("Discovered {Count} content transformer(s):", _transformers.Count);
                foreach (var t in _transformers)
                    Information("  [{Order}] {Name}", t.Order, t.Name);
            }

            return _transformers;
        }
    }

    public static string Apply(string html, Dictionary<string, string> metadata)
    {
        foreach (var transformer in Transformers)
        {
            html = transformer.Transform(html, metadata);
        }
        return html;
    }
}
