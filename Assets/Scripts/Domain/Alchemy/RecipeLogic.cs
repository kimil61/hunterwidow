using System;
using System.Collections.Generic;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Inventory;

namespace HunterWidow.Domain.Alchemy
{
    public sealed class RecipeIngredient
    {
        public RecipeIngredient(string materialId, int amount, int grade)
        {
            MaterialId = materialId;
            Amount = amount;
            Grade = grade;
        }

        public string MaterialId { get; }

        public int Amount { get; }

        public int Grade { get; }
    }

    public sealed class RecipeDefinition
    {
        public RecipeDefinition(
            string id,
            IReadOnlyList<RecipeIngredient> ingredients,
            string outputItemId,
            int outputCount,
            int durationCycles,
            int basePrice)
        {
            Id = id;
            Ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
            OutputItemId = outputItemId;
            OutputCount = outputCount;
            DurationCycles = durationCycles;
            BasePrice = basePrice;
        }

        public string Id { get; }

        public IReadOnlyList<RecipeIngredient> Ingredients { get; }

        public string OutputItemId { get; }

        public int OutputCount { get; }

        public int DurationCycles { get; }

        public int BasePrice { get; }

        public static RecipeDefinition FromContent(ContentItem item, Func<string, int> gradeResolver)
        {
            if (item == null || !string.Equals(item.Type, "recipe", StringComparison.Ordinal))
            {
                throw new ArgumentException("A recipe content item is required.", nameof(item));
            }

            var ingredients = new List<RecipeIngredient>();
            var rawIngredients = item.GetArray("ingredients");
            if (rawIngredients == null)
            {
                throw new InvalidOperationException("Recipe ingredients are missing.");
            }

            for (var ingredientIndex = 0; ingredientIndex < rawIngredients.Count; ingredientIndex++)
            {
                var rawIngredient = ContentValues.AsObject(rawIngredients[ingredientIndex]);
                if (rawIngredient == null)
                {
                    throw new InvalidOperationException("Recipe ingredient is invalid.");
                }

                var materialId = ContentValues.GetString(rawIngredient, "materialId");
                ingredients.Add(new RecipeIngredient(
                    materialId,
                    (int)ContentValues.GetNumber(rawIngredient, "amount"),
                    gradeResolver(materialId)));
            }

            return new RecipeDefinition(
                item.Id,
                ingredients,
                item.GetString("outputItemId"),
                (int)item.GetNumber("outputCount", 1d),
                (int)item.GetNumber("durationCycles", 1d),
                (int)item.GetNumber("basePrice", 0d));
        }
    }

    public sealed class RecipePreview
    {
        public RecipePreview(bool canStart, IReadOnlyList<IdCount> missingIngredients)
        {
            CanStart = canStart;
            MissingIngredients = missingIngredients;
        }

        public bool CanStart { get; }

        public IReadOnlyList<IdCount> MissingIngredients { get; }
    }

    public static class RecipeLogic
    {
        public static RecipePreview Preview(RecipeDefinition recipe, ItemLedger inventory)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            var missing = new List<IdCount>();
            for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
            {
                var ingredient = recipe.Ingredients[ingredientIndex];
                var shortage = ingredient.Amount - inventory.GetCount(ingredient.MaterialId);
                if (shortage > 0)
                {
                    missing.Add(new IdCount(ingredient.MaterialId, shortage));
                }
            }

            return new RecipePreview(missing.Count == 0, missing);
        }

        public static bool TryConsumeIngredients(RecipeDefinition recipe, ItemLedger inventory)
        {
            var preview = Preview(recipe, inventory);
            if (!preview.CanStart)
            {
                return false;
            }

            for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
            {
                var ingredient = recipe.Ingredients[ingredientIndex];
                inventory.TryRemove(ingredient.MaterialId, ingredient.Amount);
            }

            return true;
        }
    }
}
