﻿// TaskManager.cs
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
using Newtonsoft.Json;

namespace DwarfCorp
{
    /// <summary>
    /// The task manager attempts to optimally assign tasks to creatures based
    /// on feasibility and cost contraints.
    /// </summary>
    [JsonObject(IsReference = true)]
    public class TaskManager
    {

        public Faction Faction;
        public int MaxTasks = 30;

        [JsonIgnore]
        public Dictionary<Creature, Queue<Task>> TaskQueue { get; set; }

        public TaskManager()
        {
            TaskQueue = new Dictionary<Creature, Queue<Task>>();
        }

        public TaskManager(Faction faction)
        {
            Faction = faction;
            TaskQueue = new Dictionary<Creature, Queue<Task>>();
        }

        public bool TaskIsAssigned(Task goal)
        {
            return TaskQueue.SelectMany(assignment => assignment.Value).Any(t => t.Name == goal.Name); ;
        }

        public bool IsFeasible(Task task, List<CreatureAI> agents )
        {
            return agents.Any(agent => task.IsFeasible(agent.Creature));
        }

        public List<Task> CreateTasks()
        {
            List<Task> tasks = new List<Task>();

            if(Faction.Stockpiles.Count > 0)
            {
                tasks.AddRange(Faction.GatherDesignations.Select(i => new GatherItemTask(i)).Where(g => !TaskIsAssigned(g) && IsFeasible(g, Faction.Minions)));
            }

            foreach(CreatureAI creature in Faction.Minions)
            {
                if(creature.Status.Hunger.IsUnhappy())
                {
                    Task g = new SatisfyHungerTask();

                    if(IsFeasible(g, Faction.Minions))
                    {
                        tasks.Add(g);
                    }

                }
            }

            foreach (CreatureAI creature in Faction.Minions)
            {
                if (creature.Status.Energy.IsUnhappy() && PlayState.Time.IsNight())
                {
                    Task g = new SatisfyTirednessTask();

                    if (IsFeasible(g, Faction.Minions))
                    {
                        tasks.Add(g);
                    }

                }
            }

            List<Creature> threatsToRemove = new List<Creature>();
            foreach(Creature threat in Faction.Threats)
            {
                if(threat != null && !threat.IsDead)
                {
                    Task g = new KillEntityTask(threat.Physics, KillEntityTask.KillType.Auto);

                    if (!TaskIsAssigned(g) && IsFeasible(g, Faction.Minions))
                    {
                        tasks.Add(g);
                    }
                }
                else
                {
                    threatsToRemove.Add(threat);
                }
            }

            foreach (Creature threat in threatsToRemove)
            {
                Faction.Threats.Remove(threat);
            }

            foreach(BuildOrder i in Faction.DigDesignations)
            {
                if (i == null || i.Vox == null || i.Vox.Health <= 0)
                {
                    continue;
                }

                VoxelChunk chunk = PlayState.ChunkManager.ChunkData.GetVoxelChunkAtWorldLocation(i.Vox.Position);

                if(chunk != null)
                {
                    if(chunk.IsCompletelySurrounded(i.Vox))
                    {
                        continue;
                    }
                }


                Task g = new KillVoxelTask(i.Vox);

                if(!TaskIsAssigned(g) && IsFeasible(g, Faction.Minions))
                {
                    tasks.Add(g);
                }
            }

            tasks.AddRange(Faction.GuardDesignations.Select(i => new GuardVoxelTask(i.Vox)).Where(g => !TaskIsAssigned(g) && IsFeasible(g, Faction.Minions)));

            tasks.AddRange(Faction.ChopDesignations.Select(i => new KillEntityTask(i, KillEntityTask.KillType.Chop)).Where(g => !TaskIsAssigned(g) && IsFeasible(g, Faction.Minions)));

            if(Faction.Stockpiles.Count <= 0)
            {
                return tasks;
            }

            foreach(WallBuilder put in Faction.WallBuilder.Designations)
            {
                if(Faction.HasResources(new List<ResourceAmount>()
                {
                    new ResourceAmount(put.Type.ResourceToRelease)
                }))
                {

                    Task g = (new BuildVoxelTask(put.Vox, put.Type));

                    if(!TaskIsAssigned(g) && IsFeasible(g, Faction.Minions))
                    {
                        tasks.Add(g);
                    }
                }
            }

            foreach(ShipOrder ship in Faction.ShipDesignations)
            {
                List<Body> componentsToShip = new List<Body>();
                int remaining = ship.GetRemainingNumResources();

                if(remaining == 0)
                {
                    continue;
                }


                foreach(Body loc in componentsToShip)
                {
                    // TODO: Reimplement
                    /*
                    if(ship.Port.ContainsItem(loc))
                    {
                        continue;
                    }
                     */

                    Task g = new PutItemInZoneTask(Item.FindItem(loc), ship.Port);
                    
                    if(TaskIsAssigned(g) || !IsFeasible(g, Faction.Minions))
                    {
                        continue;
                    }

                    ship.Assignments.Add(g);
                    tasks.Add(g);
                }
            }

            return tasks;
        }

        public void ManageTasks()
        {
            int i = 0;
            foreach(KeyValuePair<Creature, Queue<Task>> assignment in TaskQueue)
            {
                if(assignment.Value.Count > 0)
                {
                    Task task = assignment.Value.Peek();

                    if(!assignment.Key.AI.Tasks.Contains(task))
                    {
                        assignment.Key.AI.Tasks.Add(task);
                        assignment.Value.Dequeue();
                    }
                    else
                    {
                        assignment.Value.Dequeue();
                    }
                }
                i++;
            }


        }

        public int GetMaxColumnValue(int[,] matrix, int column, int numRows, int numColumns)
        {
            int maxValue = int.MinValue;

            for(int r = 0; r < numRows; r++)
            {
                if(matrix[r, column] > maxValue)
                {
                    maxValue = matrix[r, column];
                }
            }

            return maxValue;
        }

        public int GetMaxRowValue(int[,] matrix, int row, int numRows, int numColumns)
        {
            int maxValue = int.MinValue;

            for(int c = 0; c < numColumns; c++)
            {
                if(matrix[row, c] > maxValue)
                {
                    maxValue = matrix[row, c];
                }
            }

            return maxValue;
        }

        public static int GetMax(int[,] matrix, int numRows, int numColumns)
        {
            int maxValue = int.MinValue;

            for(int c = 0; c < numColumns; c++)
            {
                for(int row = 0; row < numRows; row++)
                {
                    if(matrix[row, c] > maxValue)
                    {
                        maxValue = matrix[row, c];
                    }
                }
            }

            return maxValue;
        }

        public static void AssignTasksGreedy(List<Task> newGoals, List<CreatureAI> creatures, int maxPerGoal)
        {
            List<int> counts = new List<int>();

            for (int i = 0; i < newGoals.Count; i++)
            {
                counts.Add(0);
            }

            bool allAssigned = false;
            List<CreatureAI> randomized = new List<CreatureAI>(creatures);
            List<KeyValuePair<int, float>> costs = new List<KeyValuePair<int, float>>();
            int iters = 0;
            while (!allAssigned && iters < newGoals.Count * creatures.Count)
            {
                randomized.Shuffle();
                iters++;
                foreach (CreatureAI creature in randomized)
                {
                    costs.Clear();
                    int index = 0;
                    foreach (Task task in newGoals)
                    {
                        float cost = task.ComputeCost(creature.Creature);
                        if (!task.IsFeasible(creature.Creature))
                        {
                            cost += 1e10f;
                        }
                        costs.Add(new KeyValuePair<int, float>(index, cost));
                        index++;
                    }

                    costs.Sort((pairA, pairB) =>
                    {
                        if (pairA.Key == pairB.Key)
                        {
                            return 0;
                        }
                        else return pairA.Value.CompareTo(pairB.Value);
                    });

                    foreach (KeyValuePair<int, float> taskCost in costs)
                    {
                        if (!creature.Tasks.Contains(newGoals[taskCost.Key]) && counts[taskCost.Key] < maxPerGoal)
                        {
                            counts[taskCost.Key]++;
                            creature.Tasks.Add(newGoals[taskCost.Key].Clone());
                            break;
                        }
                    }
                    allAssigned = true;
                    foreach (int c in counts)
                    {
                        if (c == 0) allAssigned = false;
                    }
                }
            }
        }

        public static void AssignTasks(List<Task> newGoals, List<CreatureAI> creatures)
        {

            if(newGoals.Count == 0 || creatures.Count == 0)
            {
                return;
            }

            List<Task> unassignedGoals = new List<Task>();
            unassignedGoals.AddRange(newGoals);

            while(unassignedGoals.Count > 0)
            {
                int[] assignments = CalculateOptimalAssignment(unassignedGoals, creatures);
                List<Task> removals = new List<Task>();
                for(int i = 0; i < creatures.Count; i++)
                {
                    int assignment = assignments[i];

                    if (assignment >= unassignedGoals.Count)
                    {
                        continue;
                    }

                    creatures[i].Tasks.Add(unassignedGoals[assignment].Clone());
                    removals.Add(unassignedGoals[assignment]);
                }

                foreach(Task removal in removals)
                {
                    unassignedGoals.Remove(removal);
                }
            }
        }

        public static int[] CalculateOptimalAssignment(List<Task> newGoals, List<CreatureAI> agents )
        {
            int numGoals = newGoals.Count;
            int numAgents = agents.Count;
            int maxSize = Math.Max(numGoals, numAgents);

            int[,] goalMatrix = new int[maxSize, maxSize];
            const float multiplier = 100;

            if (numGoals == 0 || numAgents == 0)
            {
                return null;
            }

            for (int goalIndex = 0; goalIndex < numGoals; goalIndex++)
            {
                Task goal = newGoals[goalIndex];

                for (int agentIndex = 0; agentIndex < numAgents; agentIndex++)
                {
                    CreatureAI agent = agents[agentIndex];
                    float floatCost = goal.ComputeCost(agent.Creature);

                    int cost = (int)(floatCost * multiplier);

                    if (!goal.IsFeasible(agent.Creature))
                    {
                        cost += 99999;
                    }

                    if (agent.Creature.Status.IsAsleep)
                    {
                        cost += 99999;
                    }

                    cost += agents[agentIndex].Tasks.Count;

                    goalMatrix[agentIndex, goalIndex] = cost;
                }
            }

            // Add additional columns or rows
            if (numAgents > numGoals)
            {
                int maxValue = GetMax(goalMatrix, numAgents, numGoals) + 1;
                for (int dummyGoal = numGoals; dummyGoal < maxSize; dummyGoal++)
                {
                    for (int i = 0; i < numAgents; i++)
                    {
                        // If we have more agents than goals, we need to add additional fake goals
                        // Since goals are in columns, we are essentially adding a new column.
                        goalMatrix[i, dummyGoal] = maxValue;
                    }
                }
            }
            else if (numGoals > numAgents)
            {
                int maxValue = GetMax(goalMatrix, numAgents, numGoals) + 1;
                for (int dummyAngent = numAgents; dummyAngent < maxSize; dummyAngent++)
                {
                    for (int i = 0; i < numGoals; i++)
                    {
                        // If we have more goals than agents, we need to add additional fake agents
                        // Since goals are in columns, we are essentially adding a new row.
                        goalMatrix[dummyAngent, i] = maxValue;
                    }
                }
            }

            return goalMatrix.FindAssignments();

        }

        public void AssignTasks()
        {
            List<Task> newGoals = CreateTasks();
            int[] assignments = CalculateOptimalAssignment(newGoals, this.Faction.Minions);

            for(int i = 0; i < Faction.Minions.Count; i++)
            {
                int assignment = assignments[i];

                if(assignment >= newGoals.Count)
                {
                    continue;
                }

                if(!TaskQueue.ContainsKey(Faction.Minions[i].Creature))
                {
                    TaskQueue.Add(Faction.Minions[i].Creature, new Queue<Task>());
                }
                TaskQueue[Faction.Minions[i].Creature].Enqueue(newGoals[assignments[i]]);
            }
        }
    }

}