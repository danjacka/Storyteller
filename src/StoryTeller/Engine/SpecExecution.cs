﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using StoryTeller.Grammars;
using StoryTeller.Results;

namespace StoryTeller.Engine
{
    public interface IExecutionLogger
    {
        void Starting(IList<ILineExecution> all);

        void LineComplete(ISpecContext context, ILineExecution line);
    }

    public class NulloExecutionLogger : IExecutionLogger
    {
        public void Starting(IList<ILineExecution> all)
        {
            // nothing
        }

        public void LineComplete(ISpecContext context, ILineExecution line)
        {
            // nothing
        }
    }

    public class SpecExecution
    {
        public static void RunAll(SpecContext context, SpecificationPlan plan)
        {
            var gatherer = new LineStepGatherer(context);
            plan.AcceptVisitor(gatherer);

            foreach (var line in gatherer.Lines)
            {
                if (!context.CanContinue()) break;

                line.Execute(context);
            }
        }

        private readonly StopConditions _stopConditions;
        private readonly IExecutionLogger _logger;
        private Task _timeout;

        public SpecExecution(SpecExecutionRequest request, StopConditions stopConditions, IExecutionLogger logger)
        {
            Request = request;
            _stopConditions = stopConditions;
            _logger = logger;

        }

        public SpecExecutionRequest Request { get; }

        public Exception CatastrophicException { get; private set; }

        // If this fails, it's a catastrophic exception
        public SpecResults Execute(ISystem system, Timings timings)
        {
            _timeout = Task.Delay(_stopConditions.TimeoutInSeconds.Seconds());

            using (var execution = createExecutionContext(system, timings))
            {
                if (Request.IsCancelled)
                {
                    return null;
                }

                using (var context = new SpecContext(
                    Request.Specification,
                    timings,
                    Request.Observer,
                    _stopConditions,
                    execution))
                {
                    beforeExecution(execution, context);

                    var lines = determineLineSteps(context);

                    startDebugListening(context);

                    _logger.Starting(lines);

                    var stepRunning = Task.Factory.StartNew(() =>
                    {
                        executeSteps(context, lines);
                    }, Request.Cancellation);

                    Task.WaitAny(stepRunning, _timeout);

                    execution.AfterExecution(context);

                    return buildResults(context, timings);
                }
            }
        }

        private SpecResults buildResults(SpecContext context, Timings timings )
        {
            if (Request.IsCancelled) return null;

            var catastrophic = CatastrophicException ?? context?.CatastrophicException;
            if (catastrophic != null)
            {
                throw new StorytellerExecutionException(catastrophic);
            }

            Finished = !_timeout.IsCompleted && !Request.IsCancelled;

            if (_timeout.IsCompleted && !Request.IsCancelled)
            {
                var result = timeoutMessage(timings);

                if (context == null)
                {
                    var perf = timings.Finish();

                    return new SpecResults
                    {
                        Counts = new Counts(0, 0, 1, 0),
                        Duration = timings.Duration,
                        Performance = perf.ToArray(),
                        Attempts = Request.Plan.Attempts,
                        Results = new IResultMessage[] { result },
                        WasAborted = false
                    };
                }


                context.LogResult(result);
                context.Cancel();
            }

            return context.FinalizeResults(Request.Plan.Attempts);
        }

        private StepResult timeoutMessage(Timings timings)
        {
            return new StepResult
            {
                id = Request.Plan.Specification.id,
                status = ResultStatus.error,
                error = "Timed out in " + timings.Duration + " milliseconds",
                position = Stage.timedout
            };
        }

        private static void startDebugListening(SpecContext context)
        {
            context.Reporting.As<Reporting>().StartDebugListening();
        }

        private void beforeExecution(IExecutionContext execution, SpecContext context)
        {
            try
            {
                execution.BeforeExecution(context);
            }
            catch (Exception e)
            {
                context.LogException(Request.Id, e, "BeforeExecution");
            }
        }

        private void executeSteps(SpecContext context, IList<ILineExecution> lines)
        {
            foreach (var line in lines)
            {
                if (Request.IsCancelled || !context.CanContinue() || _timeout.IsCompleted)
                {
                    return;
                }

                execute(context, line).Wait();

                _logger.LineComplete(context, line);
            }
        }


        private IList<ILineExecution> determineLineSteps(SpecContext context)
        {
            var gatherer = new LineStepGatherer(context);
            Request.Plan.AcceptVisitor(gatherer);

            return gatherer.Lines;
        }

        private IExecutionContext createExecutionContext(ISystem system, Timings timings)
        {
            try
            {
                using (timings.Subject("Context", "Creation"))
                {
                    return system.CreateContext();
                }
            }
            catch (Exception e)
            {
                Request.Cancel();

                throw new StorytellerExecutionException(e);
            }
        }

        private Task execute(SpecContext context, ILineExecution line)
        {
            var running = Task.Factory.StartNew(() =>
            {
                line.Execute(context);
            }, Request.Cancellation);

            return Task.WhenAny(running, _timeout);
        }


        public void Cancel()
        {
            Request.Cancel();
        }
        
        public bool WasCancelled => Request.IsCancelled;

        public bool Finished { get; private set; }
    }
}