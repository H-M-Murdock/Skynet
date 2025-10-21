using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging
{
    // Entferne die lokale Test-Implementierung von ILogEvent.
    // Verwende stattdessen Skynet.Core.Logging.MutableLogEvent.

    public sealed class DefaultEnricherTests
    {
        [Fact]
        public void Generates_GlobalEventId_When_Missing()
        {
            var e = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow, GlobalEventId = "" };
            var enricher = new DefaultEnricher();
            enricher.Enrich(e);
            Assert.False(string.IsNullOrWhiteSpace(e.GlobalEventId));
        }

        [Fact]
        public void Uses_Activity_For_Correlation_And_Trace()
        {
            using var act = new Activity("test").Start();
            var e = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow };
            var enricher = new DefaultEnricher();
            enricher.Enrich(e);

            Assert.Equal(act.TraceId.ToString(), e.CorrelationId);
            Assert.Equal(act.TraceId.ToString(), e.TraceId);
            Assert.Equal(act.SpanId.ToString(), e.SpanId);
        }

        [Fact]
        public void Keeps_Existing_Correlation()
        {
            var e = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow, CorrelationId = "given" };
            var enricher = new DefaultEnricher();
            enricher.Enrich(e);
            Assert.Equal("given", e.CorrelationId);
        }
    }
}