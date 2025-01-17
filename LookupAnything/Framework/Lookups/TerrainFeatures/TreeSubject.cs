using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathoschild.Stardew.Common;
using Pathoschild.Stardew.Common.Enums;
using Pathoschild.Stardew.LookupAnything.Framework.DebugFields;
using Pathoschild.Stardew.LookupAnything.Framework.Fields;
using StardewValley;
using StardewValley.GameData.WildTrees;
using StardewValley.TerrainFeatures;

namespace Pathoschild.Stardew.LookupAnything.Framework.Lookups.TerrainFeatures
{
    /// <summary>Describes a non-fruit tree.</summary>
    internal class TreeSubject : BaseSubject
    {
        /*********
        ** Fields
        *********/
        /// <summary>The underlying target.</summary>
        private readonly Tree Target;

        /// <summary>The tree's tile position.</summary>
        private readonly Vector2 Tile;

        /// <summary>Provides subject entries.</summary>
        private readonly ISubjectRegistry Codex;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="codex">Provides subject entries.</param>
        /// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
        /// <param name="tree">The lookup target.</param>
        /// <param name="tile">The tree's tile position.</param>
        public TreeSubject(ISubjectRegistry codex, GameHelper gameHelper, Tree tree, Vector2 tile)
            : base(gameHelper, TreeSubject.GetName(tree), null, I18n.Type_Tree())
        {
            this.Codex = codex;
            this.Target = tree;
            this.Tile = tile;
        }

        /// <summary>Get the data to display for this subject.</summary>
        /// <remarks>Tree growth algorithm reverse engineered from <see cref="Tree.dayUpdate"/>.</remarks>
        public override IEnumerable<ICustomField> GetData()
        {
            Tree tree = this.Target;
            WildTreeData data = tree.GetData();
            GameLocation location = tree.Location;
            bool isFertilized = tree.fertilized.Value;

            // get growth stage
            WildTreeGrowthStage stage = (WildTreeGrowthStage)Math.Min(tree.growthStage.Value, (int)WildTreeGrowthStage.Tree);
            bool isFullyGrown = stage == WildTreeGrowthStage.Tree;
            yield return new GenericField(I18n.Tree_Stage(), isFullyGrown
                ? I18n.Tree_Stage_Done()
                : I18n.Tree_Stage_Partial(stageName: I18n.For(stage), step: (int)stage, max: (int)WildTreeGrowthStage.Tree)
            );

            // get growth schedule
            if (!isFullyGrown)
            {
                string label = I18n.Tree_NextGrowth();
                if (!data.GrowsInWinter && location.GetSeason() == Season.Winter && !location.SeedsIgnoreSeasonsHere() && !isFertilized)
                    yield return new GenericField(label, I18n.Tree_NextGrowth_Winter());
                else if (stage == WildTreeGrowthStage.Tree - 1 && this.HasAdjacentTrees(this.Tile))
                    yield return new GenericField(label, I18n.Tree_NextGrowth_AdjacentTrees());
                else
                    yield return new GenericField(label, I18n.Tree_NextGrowth_Chance(stage: I18n.For(stage + 1), chance: (isFertilized ? data.FertilizedGrowthChance : data.GrowthChance) * 100));
            }

            // get fertilizer
            if (!isFullyGrown)
            {
                if (!isFertilized)
                    yield return new GenericField(I18n.Tree_IsFertilized(), this.Stringify(false));
                else
                {
                    Item fertilizer = ItemRegistry.Create("(O)805");
                    yield return new ItemIconField(this.GameHelper, I18n.Tree_IsFertilized(), fertilizer, this.Codex);
                }
            }

            // get seed
            if (isFullyGrown && !string.IsNullOrWhiteSpace(data.SeedItemId))
            {
                string seedName = GameI18n.GetObjectName(data.SeedItemId);
                float seedChance = data.SeedChance >= 0
                    ? data.SeedChance
                    : Tree.chanceForDailySeed;
                float seedOnChopChance = data.SeedOnChopChance >= 0
                    ? data.SeedOnChopChance
                    : 0.75f;

                if (tree.hasSeed.Value)
                    yield return new ItemIconField(this.GameHelper, I18n.Tree_Seed(), ItemRegistry.Create(data.SeedItemId), this.Codex);
                else
                {
                    List<string> lines = new(2);

                    if (seedChance > 0)
                        lines.Add(I18n.Tree_Seed_ProbabilityDaily(chance: seedChance * 100, itemName: seedName));
                    if (seedOnChopChance > 0)
                        lines.Add(I18n.Tree_Seed_ProbabilityOnChop(chance: seedOnChopChance * 100, itemName: seedName));

                    if (lines.Any())
                        yield return new GenericField(I18n.Tree_Seed(), I18n.Tree_Seed_NotReady() + Environment.NewLine + string.Join(Environment.NewLine, lines));
                }
            }
        }

        /// <summary>Get the data to display for this subject.</summary>
        public override IEnumerable<IDebugField> GetDebugFields()
        {
            Tree target = this.Target;

            // pinned fields
            yield return new GenericDebugField("has seed", this.Stringify(target.hasSeed.Value), pinned: true);
            yield return new GenericDebugField("growth stage", target.growthStage.Value, pinned: true);
            yield return new GenericDebugField("health", target.health.Value, pinned: true);

            // raw fields
            foreach (IDebugField field in this.GetDebugFieldsFrom(target))
                yield return field;
        }

        /// <summary>Draw the subject portrait (if available).</summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        /// <param name="position">The position at which to draw.</param>
        /// <param name="size">The size of the portrait to draw.</param>
        /// <returns>Returns <c>true</c> if a portrait was drawn, else <c>false</c>.</returns>
        public override bool DrawPortrait(SpriteBatch spriteBatch, Vector2 position, Vector2 size)
        {
            this.Target.drawInMenu(spriteBatch, position, Vector2.Zero, 1, 1);
            return true;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get a display name for the tree.</summary>
        /// <param name="tree">The tree object.</param>
        private static string GetName(Tree tree)
        {
            string type = tree.treeType.Value;

            if (type == TreeType.BigMushroom)
                I18n.Tree_Name_BigMushroom();
            if (type == TreeType.Mahogany)
                return I18n.Tree_Name_Mahogany();
            if (type == TreeType.Maple)
                return I18n.Tree_Name_Maple();
            if (type == TreeType.Oak)
                return I18n.Tree_Name_Oak();
            if (type == TreeType.Palm)
                return I18n.Tree_Name_Palm();
            if (type == TreeType.Palm2)
                return I18n.Tree_Name_Palm();
            if (type == TreeType.Pine)
                return I18n.Tree_Name_Pine();

            return I18n.Tree_Name_Unknown();
        }

        /// <summary>Whether there are adjacent trees that prevent growth.</summary>
        /// <param name="position">The tree's position in the current location.</param>
        private bool HasAdjacentTrees(Vector2 position)
        {
            GameLocation location = Game1.currentLocation;
            return (
                from adjacentTile in Utility.getSurroundingTileLocationsArray(position)
                let otherTree = location.terrainFeatures.ContainsKey(adjacentTile)
                    ? location.terrainFeatures[adjacentTile] as Tree
                    : null
                select otherTree != null && otherTree.growthStage.Value >= (int)(WildTreeGrowthStage.Tree - 1)
            ).Any(p => p);
        }
    }
}
