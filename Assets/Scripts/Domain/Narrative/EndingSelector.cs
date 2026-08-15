using System.Collections.Generic;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Progression;

namespace HunterWidow.Domain.Narrative
{
    /// <summary>
    /// Chooses the most specific satisfied ending before presentation renders it.
    /// This keeps ending eligibility deterministic and independent from Unity UI.
    /// </summary>
    public static class EndingSelector
    {
        public static string Select(IReadOnlyList<ContentItem> endings, ProgressionState state)
        {
            if (endings == null || state == null)
            {
                return string.Empty;
            }

            ContentItem selected = null;
            var selectedConditionCount = 0;
            for (var endingIndex = 0; endingIndex < endings.Count; endingIndex++)
            {
                var candidate = endings[endingIndex];
                if (candidate == null)
                {
                    continue;
                }

                var definition = StoryEventDefinition.FromContent(candidate);
                if (!TriggerEvaluator.EvaluateAll(definition.Conditions, state)
                    || selected != null && definition.Conditions.Count <= selectedConditionCount)
                {
                    continue;
                }

                selected = candidate;
                selectedConditionCount = definition.Conditions.Count;
            }

            return selected == null ? string.Empty : selected.Id;
        }
    }
}
