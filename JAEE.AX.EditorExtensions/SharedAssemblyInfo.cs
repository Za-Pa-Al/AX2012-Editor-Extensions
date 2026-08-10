using System.Reflection;

// Single source of truth for the version of every JAEE editor-extension assembly.
// Linked into each classic (non-SDK) project by Directory.Build.targets, so bumping the
// release version is a one-line change here. The per-project AssemblyInfo.cs files keep
// their own title/description/etc. but no longer declare a version.
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]
