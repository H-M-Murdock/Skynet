// Skynet.Core/Localization/ICultureThreadScopeFactory.cs
using System;

namespace Skynet.Core.Localization
{
    public interface ICultureThreadScopeFactory
    {
        CultureThreadScope BeginScope();
    }
}