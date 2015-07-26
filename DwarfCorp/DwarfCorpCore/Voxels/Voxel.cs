﻿// Voxel.cs
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
using System.Runtime.Serialization;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;


namespace DwarfCorp
{

    /// <summary>
    /// Specifies the location of a vertex on a voxel.
    /// </summary>
    public enum VoxelVertex
    {
        FrontTopLeft,
        FrontTopRight,
        FrontBottomLeft,
        FrontBottomRight,
        BackTopLeft,
        BackTopRight,
        BackBottomLeft,
        BackBottomRight,
    }

    /// <summary>
    /// Specifies how a voxel is to be sloped.
    /// </summary>
    [Flags]
    public enum RampType
    {
        None = 0x0,
        TopFrontLeft = 0x1,
        TopFrontRight = 0x2,
        TopBackLeft = 0x4,
        TopBackRight = 0x8,
        Front = TopFrontLeft | TopFrontRight,
        Back = TopBackLeft | TopBackRight,
        Left = TopBackLeft | TopFrontLeft,
        Right = TopBackRight | TopFrontRight,
        All = TopFrontLeft | TopFrontRight | TopBackLeft | TopBackRight
    }


    /// <summary> Determines a transition texture type. Each phrase
    /// (front, left, back, right) defines whether or not a tile of the same type is
    /// on the given face</summary>
    [Flags]
    public enum TransitionTexture
    {
        None = 0,
        Front = 1,
        Right = 2,
        FrontRight = 3,
        Back = 4,
        FrontBack = 5,
        BackRight = 6,
        FrontBackRight = 7,
        Left = 8,
        FrontLeft = 9,
        LeftRight = 10,
        LeftFrontRight = 11,
        LeftBack = 12,
        FrontBackLeft = 13,
        LeftBackRight = 14,
        All = 15
    }



    /// <summary>
    /// An atomic cube in the world which represents a bit of terrain. 
    /// </summary>
    [JsonObject(IsReference = true)]
    public class Voxel : IBoundedObject
    {
        protected bool Equals(Voxel other)
        {
            return Equals(Chunk, other.Chunk) && Index == other.Index;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                return ((Chunk != null ? Chunk.GetHashCode() : 0)*397) ^ GridPosition.GetHashCode();
            }
        }

        [JsonIgnore]
        private VoxelChunk _chunk = null;


        [JsonIgnore]
        public VoxelChunk Chunk 
        {
            get { return _chunk; }
            set 
            { 
                _chunk = value;
                if (_chunk != null) ChunkID = value.ID;
            }
        }

        [JsonIgnore]
        public Vector3 Position 
        {
            get
            {
                return GridPosition + Chunk.Origin;
            }
        }


        [JsonIgnore]
        public VoxelType Type
        {
            get
            {
                if (Chunk == null) return VoxelType.TypeList[0];
                return VoxelType.TypeList[Chunk.Data.Types[Index]];
            }
            set { if(Chunk != null) Chunk.Data.Types[Index] = (byte) value.ID; }
        }

        [JsonIgnore]
        public string TypeName
        {
            get { return this.Type.Name; }
        }

        private int index = 0;
        [JsonIgnore]
        public int Index
        {
            get { return index; }
        }

        [JsonIgnore]
        public BoxPrimitive Primitive 
        {
            get { return VoxelLibrary.GetPrimitive(Type); }
        }

        [JsonIgnore]
        public bool IsVisible 
        {
            get { return  GridPosition.Y <= Chunk.Manager.ChunkData.MaxViewingLevel; }
        }

        [JsonIgnore]
        public bool IsExplored
        {
            get { return !GameSettings.Default.FogofWar || Chunk.Data.IsExplored[Index]; }
            set { Chunk.Data.IsExplored[Index] = value; }
        }

        private Vector3 gridpos = Vector3.Zero;

        public Vector3 GridPosition
        {
            get { return gridpos; }
            set 
            { 
                gridpos = value;

                if(Chunk != null)
                    index = Chunk.Data.IndexAt((int)gridpos.X, (int)gridpos.Y, (int)gridpos.Z); 
            }
        }


        [JsonIgnore]
        public static List<VoxelVertex> VoxelVertexList { get; set; }
        private static bool staticsCreated;

        [JsonIgnore]
        public bool IsDead
        {
            get { return Health <= 0; }
        }

        [JsonIgnore]
        public RampType RampType
        {
            get { return Chunk.Data.RampTypes[Index]; }
            set { Chunk.Data.RampTypes[Index] = value; }
        }

        [JsonIgnore]
        public bool IsInterior
        {
            get { return Chunk.IsInterior((int) GridPosition.X, (int) GridPosition.Y, (int) GridPosition.Z); }
        }
        private static readonly Color BlankColor = new Color(0, 255, 0);

        private Point3 chunkID = new Point3(0, 0, 0);
        public Point3 ChunkID
        {
            get { return chunkID; }
            set { chunkID = value;  }
        }

        [JsonIgnore]
        public float Health
        {
            get { return (float) Chunk.Data.Health[Index]; }
            set
            {
                if (Type.IsInvincible) return;
                Chunk.Data.Health[Index] = (byte)value;

                if (value <= 0.0f)
                {
                    Kill();
                }
            }
        }

      
        public uint GetID()
        {
            return (uint) GetHashCode();
        }


        public bool IsTopEmpty()
        {
            if(GridPosition.Y >= Chunk.SizeY)
            {
                return true;
            }
            return
                Chunk.Data.Types[
                    Chunk.Data.IndexAt((int) GridPosition.X, (int) GridPosition.Y + 1, (int) GridPosition.Z)] == 0;
        }

        public Voxel GetVoxelAbove()
        {
            if (GridPosition.Y >= Chunk.SizeY)
            {
                return null;
            }
            return
                Chunk.MakeVoxel((int) GridPosition.X, (int) GridPosition.Y + 1, (int) GridPosition.Z);
        }

        public bool IsBottomEmpty()
        {
            if (GridPosition.Y <= 0)
            {
                return true;
            }
            return
                Chunk.Data.Types[
                    Chunk.Data.IndexAt((int)GridPosition.X, (int)GridPosition.Y - 1, (int)GridPosition.Z)] == 0;
        }

        public static bool IsInteriorPoint(Point3 gridPosition, VoxelChunk chunk)
        {
            return chunk.IsInterior(gridPosition.X, gridPosition.Y, gridPosition.Z);
        }

        public static bool HasFlag(RampType ramp, RampType flag)
        {
            return (ramp & flag) == flag;
        }
       
        [JsonIgnore]
        public bool IsEmpty
        {
            get { return Type.ID == 0; }
        }

        [JsonIgnore]
        public int SunColor { get { return Chunk.Data.SunColors[Index]; }}

        public void SetFromData(VoxelChunk chunk, Vector3 gridPosition)
        {
            Chunk = chunk;
            GridPosition = gridPosition;
            index = Chunk.Data.IndexAt((int) gridPosition.X, (int) gridPosition.Y, (int) gridPosition.Z);
        }

        public override bool Equals(object o)
        {
            if (ReferenceEquals(null, o)) return false;
            if (ReferenceEquals(this, o)) return true;
            if (o.GetType() != this.GetType()) return false;
            return Equals((Voxel) o);
        }

        public void UpdateStatics()
        {
            if(staticsCreated)
            {
                return;
            }

            VoxelVertexList = new List<VoxelVertex>
            {
                VoxelVertex.BackBottomLeft,
                VoxelVertex.BackBottomRight,
                VoxelVertex.BackTopLeft,
                VoxelVertex.BackTopRight,
                VoxelVertex.FrontBottomRight,
                VoxelVertex.FrontBottomLeft,
                VoxelVertex.FrontTopRight,
                VoxelVertex.FrontTopLeft
            };
            staticsCreated = true;
        }



        public void Kill()
        {
            if (IsEmpty)
            {
                return;
            }

            if(PlayState.ParticleManager != null)
            {
                PlayState.ParticleManager.Trigger(Type.ParticleType, Position + new Vector3(0.5f, 0.5f, 0.5f), Color.White, 20);
                PlayState.ParticleManager.Trigger("puff", Position + new Vector3(0.5f, 0.5f, 0.5f), Color.White, 20);
            }

            if(PlayState.Master != null)
            {
                PlayState.Master.Faction.OnVoxelDestroyed(this);
            }

            SoundManager.PlaySound(Type.ExplosionSound, Position);
            if (Type.ReleasesResource)
            {
                float randFloat = MathFunctions.Rand();

                if (randFloat < Type.ProbabilityOfRelease)
                {
                    EntityFactory.CreateEntity<Body>(Type.ResourceToRelease + " Resource", Position + new Vector3(0.5f, 0.5f, 0.5f));
                }
            }

            Chunk.Manager.KilledVoxels.Add(this);
            Chunk.Data.Types[Index] = 0; 
        }

        public BoundingSphere GetBoundingSphere()
        {
            return new BoundingSphere(Position, 1);
        }

        public BoundingBox GetBoundingBox()
        {
            BoundingBox pBox = Primitive != null ? Primitive.BoundingBox : new BoundingBox(Vector3.Zero, new Vector3(1, 1, 1));
            return new BoundingBox(pBox.Min + Position, pBox.Max + Position);
        }

        public Voxel()
        {
            
        }

        public Voxel(Point3 gridPosition, VoxelChunk chunk)
        {
            UpdateStatics();
            Chunk = chunk;
            chunkID = chunk.ID;
            GridPosition = new Vector3(gridPosition.X, gridPosition.Y, gridPosition.Z);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (PlayState.ChunkManager.ChunkData.ChunkMap.ContainsKey(chunkID))
            {
                Chunk = PlayState.ChunkManager.ChunkData.ChunkMap[chunkID];
                index = Chunk.Data.IndexAt((int) GridPosition.X, (int) GridPosition.Y, (int) GridPosition.Z);
            }
        }

        public TransitionTexture ComputeTransitionValue(Voxel[] manhattanNeighbors)
        {
            return Chunk.ComputeTransitionValue((int) GridPosition.X, (int) GridPosition.Y, (int) GridPosition.Z, manhattanNeighbors);
        }

        public BoxPrimitive.BoxTextureCoords ComputeTransitionTexture(Voxel[] manhattanNeighbors)
        {
            if(!Type.HasTransitionTextures && Primitive != null)
            {
                return Primitive.UVs;
            }
            else if(Primitive == null)
            {
                return null;
            }
            else
            {
                return Type.TransitionTextures[ComputeTransitionValue(manhattanNeighbors)];
            }
        }

        [JsonIgnore]
        public WaterCell Water
        {
            get { return Chunk.Data.Water[Index]; }
            set { Chunk.Data.Water[Index] = value; }
        }

        [JsonIgnore]
        public byte WaterLevel
        {
            get { return Water.WaterLevel; }
            set
            {
                WaterCell cell = Water;
                cell.WaterLevel = value;
                Chunk.Data.Water[Index] = cell;
            }
        }
    }

}