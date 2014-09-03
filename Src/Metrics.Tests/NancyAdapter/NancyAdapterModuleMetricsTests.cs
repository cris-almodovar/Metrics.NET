﻿using FluentAssertions;
using Metrics.Tests.TestUtils;
using Metrics.Utils;
using Nancy;
using Nancy.Metrics;
using Nancy.Testing;
using Xunit;

namespace Metrics.Tests.NancyAdapter
{
    public class NancyAdapterModuleMetricsTests
    {
        public class TestModule : NancyModule
        {
            public TestModule(TestClock clock)
                : base("/test")
            {
                this.MetricForRequestTimeAndResponseSize("Action Request", "Get", "/");
                this.MetricForRequestSize("Request Size", "Put", "/");

                Get["/action"] = _ =>
                {
                    clock.Advance(TimeUnit.Milliseconds, 100);
                    return Response.AsText("response");
                };

                Get["/contentWithLength"] = _ =>
                {
                    clock.Advance(TimeUnit.Milliseconds, 100);
                    return Response.AsText("response").WithHeader("Content-Length", "100");
                };

                Put["/size"] = _ => HttpStatusCode.OK;
            }
        }

        private readonly TestContext context = new TestContext();
        private readonly Browser browser;

        public NancyAdapterModuleMetricsTests()
        {
            this.context.Config.WithNancy(c => { });

            this.browser = new Browser(with =>
            {
                with.Module(new TestModule(this.context.Clock));
            });
        }


        [Fact]
        public void NancyMetricsShouldBeAbleToMonitorTimeForModuleRequest()
        {
            this.context.TimerValue("Action Request").Rate.Count.Should().Be(0);
            browser.Get("/test/action").StatusCode.Should().Be(HttpStatusCode.OK);

            var timer = this.context.TimerValue("Action Request");

            timer.Rate.Count.Should().Be(1);
            timer.Histogram.Count.Should().Be(1);
            timer.Histogram.Max.Should().Be(TimeUnit.Milliseconds.ToNanoseconds(100));
        }

        [Fact]
        public void NancyMetricsShouldBeAbleToMonitorSizeForRouteReponse()
        {
            browser.Get("/test/action").StatusCode.Should().Be(HttpStatusCode.OK);

            var sizeHistogram = this.context.HistogramValue("Action Request");

            sizeHistogram.Count.Should().Be(1);
            sizeHistogram.Min.Should().Be("response".Length);
            sizeHistogram.Max.Should().Be("response".Length);

            browser.Get("/test/contentWithLength").StatusCode.Should().Be(HttpStatusCode.OK);

            sizeHistogram = this.context.HistogramValue("Action Request");

            sizeHistogram.Count.Should().Be(2);
            sizeHistogram.Min.Should().Be("response".Length);
            sizeHistogram.Max.Should().Be(100);
        }

        [Fact]
        public void NancyMetricsShouldBeAbleToMonitorSizeForRequest()
        {
            this.context.HistogramValue("Request Size").Count.Should().Be(0);

            browser.Put("/test/size", ctx =>
            {
                ctx.Header("Content-Length", "content".Length.ToString());
                ctx.Body("content");
            }).StatusCode.Should().Be(HttpStatusCode.OK);

            var sizeHistogram = this.context.HistogramValue("Request Size");

            sizeHistogram.Count.Should().Be(1);
            sizeHistogram.Min.Should().Be("content".Length);
        }
    }
}
