using AutoMapper;

namespace AutoMapperAnalyzer.Samples.AnalyzerOnly;

/// <summary>
///     Analyzer-only sample for the AM030 invalid converter implementation descriptor.
/// </summary>
public class MissingConvertStringToDateTimeConverter : ITypeConverter<string, DateTime>
{
}
