using System;
using System.Collections.Generic;
using HunterWidow.Domain.Progression;

namespace HunterWidow.Domain.Narrative
{
    public sealed class StoryDirector
    {
        private readonly List<StoryEventDefinition> events;
        private readonly HashSet<string> firedEvents = new HashSet<string>(StringComparer.Ordinal);

        public StoryDirector(IReadOnlyList<StoryEventDefinition> events)
        {
            this.events = new List<StoryEventDefinition>(events ?? throw new ArgumentNullException(nameof(events)));
            this.events.Sort((left, right) =>
            {
                var priorityComparison = right.Priority.CompareTo(left.Priority);
                return priorityComparison != 0 ? priorityComparison : string.CompareOrdinal(left.Id, right.Id);
            });
        }

        public IReadOnlyList<StoryEventDefinition> EvaluateTownReturn(NarrativeExecutionContext context)
        {
            var fired = new List<StoryEventDefinition>();
            for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                var storyEvent = events[eventIndex];
                if (firedEvents.Contains(storyEvent.Id) || !TriggerEvaluator.EvaluateAll(storyEvent.Conditions, context.State))
                {
                    continue;
                }

                for (var effectIndex = 0; effectIndex < storyEvent.Effects.Count; effectIndex++)
                {
                    TriggerEvaluator.Apply(storyEvent.Effects[effectIndex], context);
                }

                firedEvents.Add(storyEvent.Id);
                fired.Add(storyEvent);
            }

            return fired;
        }

        public bool HasFired(string eventId)
        {
            return firedEvents.Contains(eventId);
        }
    }
}
