// System Basics
global using System;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Collections.Concurrent;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;

// Domain Primitives
global using Skynet.Core.Tenant;
global using Skynet.Core.ResourceProvider;

// Optional: Falls du "InternalsVisibleTo" hier definieren willst statt in der Projektdatei
// (Ersetze "Skynet.Tests" mit dem echten Namen deines Testprojekts)
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Skynet.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Skynet.Core.Tests")]
