namespace RTTUnitEditor.Editing {
 // Future boundaries only; no adapter, importer, renderer or invocation path.
 public interface IExternalDefinitionSource { string DescribeSource(); }
 public interface IThreeDimensionalPreview { bool IsAvailable { get; } }
}
