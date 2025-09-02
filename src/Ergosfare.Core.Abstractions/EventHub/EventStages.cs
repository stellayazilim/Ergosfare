namespace Ergosfare.Core.Abstractions.EventHub;

public enum EventStage
{
    
    // start of pipeline
    PipeLineStarted,
    
    // Pre-Interceptor Stages
    BeforeAllPreInterception,
    BeforePreInterception,
    AfterPreInterception,
    AfterAllPreInterception,

    // Handler Stages
    BeforeAllHandler,
    BeforeHandler,
    AfterHandler,
    AfterAllHandler,

    // Post-Interceptor Stages
    BeforeAllPostInterception,
    BeforePostInterception,
    AfterPostInterception,
    AfterAllPostInterception,

    // Exception-Interceptor Stages
    BeforeAllExceptionInterception,
    BeforeExceptionInterception,
    AfterExceptionInterception,
    AfterAllExceptionInterception,
    
    // end of pipeline
    PipelineFinished
}