﻿// Shadow.cs
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarfCorp
{

    /// <summary>
    /// This component projects a billboard shadow to the ground below an entity.
    /// </summary>
    public class Shadow : Sprite
    {
        public float GlobalScale { get; set; }
        public Timer UpdateTimer { get; set; }
        private Matrix OriginalTransform { get; set; }

        public Shadow() : base()
        {
            
        }

        public Shadow(ComponentManager manager, string name, GameComponent parent, Matrix localTransform, SpriteSheet spriteSheet) :
            base(manager, name, parent, localTransform, spriteSheet, false)
        {
            OrientationType = OrientMode.Fixed;
            GlobalScale = LocalTransform.Left.Length();
            LightsWithVoxels = false;
            UpdateTimer = new Timer(0.5f, false);
            Tint = Color.Black;
            OriginalTransform = LocalTransform;
        }

        public override void Update(DwarfTime gameTime, ChunkManager chunks, Camera camera)
        {
            UpdateTimer.Update(gameTime);
            if(HasMoved && UpdateTimer.HasTriggered)
            {
                Body p = (Body) Parent;

                VoxelChunk chunk = chunks.ChunkData.GetVoxelChunkAtWorldLocation(p.GlobalTransform.Translation);

                if(chunk != null)
                {
                    Vector3 g = chunk.WorldToGrid(p.GlobalTransform.Translation + Vector3.Down * 0.25f);

                    int h = chunk.GetFilledVoxelGridHeightAt((int) g.X, (int) g.Y, (int) g.Z);

                    if(h != -1)
                    {
                        Vector3 pos = p.GlobalTransform.Translation;
                        pos.Y = h;
                        pos += VertexNoise.GetNoiseVectorFromRepeatingTexture(pos + Vector3.Down * 0.25f);
                        float scaleFactor = GlobalScale / (Math.Max((p.GlobalTransform.Translation.Y - h) * 0.25f, 1));
                        Matrix newTrans = OriginalTransform;
                        newTrans *= Matrix.CreateScale(scaleFactor);
                        newTrans.Translation = (pos - p.GlobalTransform.Translation) + new Vector3(0.0f, 0.1f, 0.0f);
                        Tint = new Color(Tint.R, Tint.G, Tint.B, (int)(scaleFactor * 255));
                        LocalTransform = newTrans;
                    }
                }
                UpdateTimer.HasTriggered = false;
            }


            base.Update(gameTime, chunks, camera);
        }
    }

}