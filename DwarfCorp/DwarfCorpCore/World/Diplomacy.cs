﻿// Diplomacy.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class Diplomacy
    {
        public class PoliticalEvent
        {
            public float Change { get; set; }
            public string Description { get; set; }
            public TimeSpan Duration { get; set; }
            public DateTime Time { get; set; }
        }

        public class Politics
        {
            public Faction Faction { get; set; }
            public List<PoliticalEvent> RecentEvents { get; set; }
            public bool HasMet { get; set; }

            public Relationship GetCurrentRelationship()
            {
                float feeling = GetCurrentFeeling();

                if (feeling < -0.5f)
                {
                    return Relationship.Hates;
                }
                else if (feeling < 0.5f)
                {
                    return Relationship.Indifferent;
                }
                else
                {
                    return Relationship.Loves;
                }
            }

            public float GetCurrentFeeling()
            {
                return RecentEvents.Sum(e => e.Change);
            }

            public void UpdateEvents(DateTime currentDate)
            {
                RecentEvents.RemoveAll((e) => currentDate - e.Time > e.Duration);
            }
        }

        public FactionLibrary Factions { get; set; }
        public Dictionary<Pair<Faction>, Politics> FactionPolitics { get; set; }

        public Diplomacy(FactionLibrary factions)
        {
            Factions = factions;
            FactionPolitics = new Dictionary<Pair<Faction>, Politics>();
        }

        public Politics GetPolitics(Faction factionA, Faction factionB)
        {
            return FactionPolitics[new Pair<Faction>(factionA, factionB)];
        }

        public void Initialize(DateTime now)
        {
            TimeSpan forever = new TimeSpan(999999, 0, 0, 0);
            foreach (var faction in Factions.Factions)
            {
                foreach (var otherFaction in Factions.Factions)
                {
                    Pair<Faction> pair = new Pair<Faction>(faction.Value, otherFaction.Value);

                    if (FactionPolitics.ContainsKey(pair)) 
                        continue;

                    if (faction.Key == otherFaction.Key)
                    {
                        FactionPolitics[pair] = new Politics()
                        {
                            Faction = faction.Value,
                            HasMet = true,
                            RecentEvents = new List<PoliticalEvent>()
                            {
                                new PoliticalEvent()
                                {
                                    Change = 1.0f,
                                    Description = "We belong to the same faction.", 
                                    Duration = forever,
                                    Time = now
                                }
                            }
                        };
                    }
                    else
                    {
                        Politics politics = new Politics()
                        {
                            Faction = otherFaction.Value,
                            HasMet = false,
                            RecentEvents = new List<PoliticalEvent>()
                        };

                        if (faction.Value.Race == otherFaction.Value.Race)
                        {
                            politics.RecentEvents.Add(new PoliticalEvent()
                            {
                                Change = 0.5f,
                                Description = "We belong to the same race.",
                                Duration = forever,
                                Time = now
                            });

                        }
                        FactionPolitics[pair] = politics;
                    }

                }
            }
        }

        public void SendTradeEnvoy(Faction natives)
        {
            // todo
            PlayState playState = (PlayState) GameState.Game.StateManager.GetState<PlayState>("PlayState");

            if (playState.IsActiveState)
            {
                List<CreatureAI> creatures =
                    PlayState.MonsterSpawner.Spawn(PlayState.MonsterSpawner.GenerateSpawnEvent(natives,
                        PlayState.PlayerFaction, PlayState.Random.Next(4) + 1, false));

                if (creatures.Count > 0)
                {
                    PlayState.AnnouncementManager.Announce("Trade envoy from " + natives.Name + " has arrived!",
                        "Click to zoom to location", creatures.First().ZoomToMe);
                }

                GameState.Game.StateManager.PushState(new DiplomacyState(GameState.Game, GameState.Game.StateManager,
                    (PlayState) GameState.Game.StateManager.GetState<PlayState>("PlayState"), natives) { Name = "DiplomacyState_" + natives.Name});
            }
        }

        public void SendWarParty(Faction natives)
        {
            // todo
            PlayState.AnnouncementManager.Announce("War party from " + natives.Name + " has arrived!", "");
            Politics politics = GetPolitics(natives, PlayState.PlayerFaction);
            PlayState.MonsterSpawner.Spawn(PlayState.MonsterSpawner.GenerateSpawnEvent(natives, PlayState.PlayerFaction, PlayState.Random.Next(5) + 1, true));
        }


        public void Update(DwarfTime time, DateTime currentDate)
        {
            foreach (var mypolitics in FactionPolitics)
            {
                Pair<Faction> pair = mypolitics.Key;
                if (!pair.IsSelfPair() && pair.Contains(PlayState.PlayerFaction))
                {
                    Faction otherFaction = null;

                    otherFaction = pair.First.Equals(PlayState.PlayerFaction) ? pair.Second : pair.First;

                    Race race = otherFaction.Race;
                    Politics relation = mypolitics.Value;

                    if (race.IsIntelligent && race.IsNative && !otherFaction.IsRaceFaction && !relation.HasMet && MathFunctions.RandEvent(1e-2f))
                    {
                        SendTradeEnvoy(otherFaction);
                    }

                    if (race.IsIntelligent && race.IsNative && !otherFaction.IsRaceFaction && relation.GetCurrentRelationship() == Relationship.Hates && MathFunctions.RandEvent(1e-7f))
                    {
                        SendWarParty(otherFaction);
                    }
                }
                mypolitics.Value.UpdateEvents(currentDate);
            }
        }
    }
}
