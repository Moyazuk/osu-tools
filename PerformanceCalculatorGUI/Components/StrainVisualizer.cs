// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using PerformanceCalculatorGUI.Components.TextBoxes;

namespace PerformanceCalculatorGUI.Components
{
    public partial class StrainVisualizer : Container
    {
        public readonly Bindable<Skill[]> Skills = new Bindable<Skill[]>();

        private readonly List<Bindable<bool>> graphToggles = new List<Bindable<bool>>();

        public readonly Bindable<int> TimeUntilFirstStrain = new Bindable<int>();

        private ZoomableScrollContainer graphsContainer;
        private FillFlowContainer legendContainer;

        private ColourInfo[] skillColours;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; }

        public StrainVisualizer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        private float graphAlpha;

        private void updateGraphs(ValueChangedEvent<Skill[]> val)
        {
            graphsContainer.Clear();

            var skills = val.NewValue.Where(x => x is StrainSkill or StrainDecaySkill or OsuProbabilitySkill).ToArray();

            // dont bother if there are no strain skills to draw
            if (skills.Length == 0)
            {
                legendContainer.Clear();
                graphToggles.Clear();
                return;
            }

            graphAlpha = Math.Min(1.5f / skills.Length, 0.9f);
            var (strainLists, timestamps) = getStrainLists(skills);
            for (int i = 0; i < skills.Length; i++)
            {
                if (timestamps != null && i < timestamps.Count && timestamps[i] != null)
                {
                    //  if we're placing individual note timestamps
                    addNoteBarsWithTimestamps(i, strainLists[i], timestamps[i]);
                }
                else
                {
                    // if we don't have timestamps, don't use them.
                    addNoteBarsWithoutTimestamps(i, strainLists[i]);
                }
            }
            addTooltipBars(strainLists, timestamps);

            if (val.OldValue == null || !val.NewValue.All(x => val.OldValue.Any(y => y.GetType().Name == x.GetType().Name)))
            {
                // skill list changed - recreate toggles
                legendContainer.Clear();
                graphToggles.Clear();

                for (int i = 0; i < skills.Length; i++)
                {
                    // this is ugly, but it works
                    var graphToggleBindable = new Bindable<bool>();
                    var graphNum = i;
                    graphToggleBindable.BindValueChanged(state =>
                    {
                        if (state.NewValue)
                        {
                            graphsContainer[graphNum].FadeTo(graphAlpha);
                        }
                        else
                        {
                            graphsContainer[graphNum].Hide();
                        }
                    });
                    graphToggles.Add(graphToggleBindable);

                    legendContainer.Add(new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Masking = true,
                        CornerRadius = 10,
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background5
                            },
                            new ExtendedOsuCheckbox
                            {
                                Padding = new MarginPadding(10),
                                RelativeSizeAxes = Axes.None,
                                Width = 200,
                                Current = { BindTarget = graphToggleBindable, Default = true, Value = true },
                                LabelText = skills[i].GetType().Name,
                                TextColour = skillColours[i]
                            }
                        }
                    });
                }
            }
            else
            {
                for (int i = 0; i < skills.Length; i++)
                {
                    // graphs are visible by default, we want to hide ones that were disabled before
                    if (!graphToggles[i].Value)
                        graphsContainer[i].Hide();
                }
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            skillColours = new ColourInfo[]
            {
                colours.Blue,
                colours.Green,
                colours.Red,
                colours.Yellow,
                colours.Pink,
                colours.Cyan
            };

            Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = ExtendedLabelledTextBox.CORNER_RADIUS,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                        Alpha = 0.6f
                    },
                    new FillFlowContainer
                    {
                        Padding = new MarginPadding(10),
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            graphsContainer = new ZoomableScrollContainer(1, 100, 1)
                            {
                                Height = 150,
                                RelativeSizeAxes = Axes.X
                            },
                            legendContainer = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(5)
                            }
                        }
                    }
                }
            });

            Skills.BindValueChanged(updateGraphs);
        }

        private void addNoteBarsWithTimestamps(int index, float[] strainLists, float[] timestamps)
        {
            var strainMaxValue = strainLists.Max();

            graphsContainer.AddRange(new Drawable[]
            {
                new BufferedContainer(cachedFrameBuffer: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = graphAlpha,
                    Colour = skillColours[index],
                    Child = new StrainBarGraph
                    {
                        RelativeSizeAxes = Axes.Both,
                        MaxValue = strainMaxValue,
                        Values = strainLists,
                        Timestamps = timestamps
                    }
                }
            });
        }

        private void addNoteBarsWithoutTimestamps(int index, float[] strainLists)
        {
            var strainMaxValue = strainLists.Max();

            graphsContainer.AddRange(new Drawable[]
            {
                new BufferedContainer(cachedFrameBuffer: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = graphAlpha,
                    Colour = skillColours[index],
                    Child = new StrainBarGraph
                    {
                        RelativeSizeAxes = Axes.Both,
                        MaxValue = strainMaxValue,
                        Values = strainLists,
                    }
                }
            });

            graphsContainer.Add(new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 10),
                Text = $"{strainMaxValue:0.00}"
            });
        }

    private void addTooltipBars(List<float[]> strainLists, List<float[]>? timestamps = null, int nBars = 1000)
    {
        double lastStrainTime;

        if (timestamps != null && timestamps.Any())
        {

            lastStrainTime = timestamps.Max(t => t.LastOrDefault());
        }
        else
        {

            lastStrainTime = strainLists.Max(l => l.Length) * 400;
        }

        var tooltipList = new List<string>();

        for (int i = 0; i < nBars; i++)
        {

            var strainTime = TimeSpan.FromMilliseconds(TimeUntilFirstStrain.Value + lastStrainTime * i / nBars);
            var tooltipText = $"~{strainTime:mm\\:ss\\.ff}";
            tooltipList.Add(tooltipText);
        }

        graphsContainer.AddRange(new Drawable[]
        {
            new BufferedContainer(cachedFrameBuffer: true)
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 1,
                Child = new TooltipBarGraph
                {
                    RelativeSizeAxes = Axes.Both,
                    Values = tooltipList
                }
            }
        });
    }

        private static (List<float[]> StrainLists, List<float[]>? Timestamps) getStrainLists(Skill[] skills)
        {
            var strainLists = new List<float[]>();
            var timestamps = new List<float[]>();
            foreach (var skill in skills)
            {
                if (skill is OsuProbabilitySkill probSkill)
                {
                    var data = probSkill.GetCurrentStrainPeaks();
                    strainLists.Add(data.Select(d => (float)d.Value).ToArray());
                    timestamps.Add(data.Select(d => (float)d.Timestamp).ToArray());
                }
                else if (skill is StrainSkill strainSkill)
                {
                    strainLists.Add(strainSkill.GetCurrentStrainPeaks().Select(d => (float)d).ToArray());
                }
            }

            return (strainLists, timestamps.Count > 0 ? timestamps : null);
        }
    }

    public partial class StrainBarGraph : FillFlowContainer<Bar>
    {
        /// <summary>
        /// Manually sets the max value, if null <see cref="Enumerable.Max(IEnumerable{float})"/> is instead used
        /// </summary>
        public float? MaxValue { get; set; }

        /// <summary>
        /// A list of floats that defines the length of each <see cref="Bar"/>
        /// </summary>
        public IEnumerable<float> Values { get; set; }

        /// <summary>
        /// Nullable timestamps for per note skills
        /// </summary>
        public IEnumerable<float>? Timestamps { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            UpdateBars();
        }

        private void UpdateBars()
        {
            Clear();

            if (Values == null || !Values.Any())
                return;

            float[] valuesArray = Values.ToArray();
            float maxValue = MaxValue ?? valuesArray.Max();

            if (Timestamps != null)
            {
                // for per note skills
                float[] localTimestampsArray = Timestamps.ToArray();
                if (localTimestampsArray.Length == 0 || localTimestampsArray.Last() == 0)
                {
                    throw new ArgumentException("Timestamps are empty or have an invalid range.");
                }

                float totalDuration = localTimestampsArray.Last();
                float shortestInterval = float.MaxValue;

                // probably not the best way to do this, but tries to get the best bar width by calculating the shortest straintime in the map
                for (int i = 1; i < localTimestampsArray.Length; i++)
                {
                    float interval = localTimestampsArray[i] - localTimestampsArray[i - 1];
                    if (interval > 0) shortestInterval = Math.Min(shortestInterval, interval);
                }

                if (shortestInterval <= 0)
                    throw new ArgumentException("Invalid timestamp intervals.");

                int totalBars = (int)Math.Ceiling(totalDuration / shortestInterval);
                float barWidth = 1.0f / totalBars;

                int currentTimestampIndex = 0;
                int totalBarsLogged = 0;


                for (int i = 0; i < totalBars; i++)
                {
                    float currentTime = i * shortestInterval;

                    // matching current position to actual note location
                    if (currentTimestampIndex < localTimestampsArray.Length &&
                        Math.Abs(currentTime - localTimestampsArray[currentTimestampIndex]) < shortestInterval / 2)
                    {
                        // put a bar
                        Add(new Bar
                        {
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(barWidth, 1),
                            Length = valuesArray[currentTimestampIndex] / maxValue,
                            Direction = BarDirection.BottomToTop,
                        });

                        // Console.WriteLine($"[BAR {totalBarsLogged + 1}] Note: Timestamp={TimeSpan.FromMilliseconds(localTimestampsArray[currentTimestampIndex]):mm\\:ss}, Value={valuesArray[currentTimestampIndex]}, Value={valuesArray[currentTimestampIndex]}, Index={currentTimestampIndex + 1}/{localTimestampsArray.Length}");

                        currentTimestampIndex++;
                    }
                    else
                    {
                        // skip this timestamp if there's note with an empty bar
                        Add(new Bar
                        {
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(barWidth, 1),
                            Length = 0,
                            Direction = BarDirection.BottomToTop,
                        });

                        // Console.WriteLine($"[BAR {totalBarsLogged + 1}] Empty: Timestamp={TimeSpan.FromMilliseconds(localTimestampsArray[currentTimestampIndex]):mm\\:ss}, Value={valuesArray[currentTimestampIndex]} (not aligned to note)");
                    }
                    totalBarsLogged++;
                }
            }
            else
            {
                // StrainSkill: Uniformly space bars
                float barWidth = 1.0f / valuesArray.Length;

                for (int i = 0; i < valuesArray.Length; i++)
                {
                    Add(new Bar
                    {
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(barWidth, 1),
                        Length = valuesArray[i] / maxValue,
                        Direction = BarDirection.BottomToTop,
                    });
                }
            }
        }
    }

    public partial class TooltipBar : Bar, IHasTooltip
    {
        public TooltipBar(string tooltip)
        {
            TooltipText = tooltip;
        }

        public LocalisableString TooltipText { get; }
    }

    public partial class TooltipBarGraph : FillFlowContainer<TooltipBar>
    {
        /// <summary>
        /// A list of strings that defines tooltips, don't make it too big
        /// </summary>
        public IEnumerable<string> Values
        {
            set
            {
                Clear();

                foreach (var tooltip in value)
                {
                    float size = value.Count();
                    if (size != 0)
                        size = 1.0f / size;

                    Add(new TooltipBar(tooltip)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(size, 1),
                        Direction = BarDirection.BottomToTop
                    });
                }
            }
        }
    }
}
