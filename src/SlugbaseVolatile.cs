using SlugBase;
using System;
using System.Linq;
using UnityEngine;
using RWCustom;
using MonoMod.Cil;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;
using System.IO;
using System.ComponentModel;

namespace TheVolatile
{
    public class SlugbaseVolatile : SlugBaseCharacter
    {
        public static SlugbaseVolatile instance;

        

        public SlugbaseVolatile() : base("The Volatile", FormatVersion.V1, 0, true)
        {
            instance = this;
            On.Player.ctor += Player_ctor;
            On.Player.Update += Player_Update;
            On.Player.Die += Player_Die;
            On.Creature.Violence += Creature_Violence;
            On.PlayerGraphics.ctor += PlayerGraphics_ctor;
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
            //IL.Player.ThrowObject += IL_Player_ThrowObject;
            On.Player.ReleaseGrasp += Player_ReleaseGrasp;
            On.Player.CanBeSwallowed += Player_CanBeSwallowed;
            On.Player.Grabability += Player_Grabability;
            On.Player.SlugcatGrab += Player_SlugcatGrab;
            On.SharedPhysics.TraceProjectileAgainstBodyChunks += SharedPhysics_TraceProjectileAgainstBodyChunks;

            On.GameSession.ctor += GameSession_ctor;
            On.ScavengerAI.CollectScore_PhysicalObject_bool += ScavengerAI_CollectScore_PhysicalObject_bool;
        }

        private SharedPhysics.CollisionResult SharedPhysics_TraceProjectileAgainstBodyChunks(On.SharedPhysics.orig_TraceProjectileAgainstBodyChunks orig, SharedPhysics.IProjectileTracer projTracer, Room room, Vector2 lastPos, ref Vector2 pos, float rad, int collisionLayer, PhysicalObject exemptObject, bool hitAppendages)
        {
            if (projTracer is Lighter) rad *= 2.5f;
            return orig(projTracer, room, lastPos, ref pos, rad, collisionLayer, exemptObject, hitAppendages);
        }

        private void Player_Die(On.Player.orig_Die orig, Player self)
        {
            if (IsMe(self)) {
                Lighter.getMine(self).Destroy();
            }
            orig(self);
        }

        private int ScavengerAI_CollectScore_PhysicalObject_bool(On.ScavengerAI.orig_CollectScore_PhysicalObject_bool orig, ScavengerAI self, PhysicalObject obj, bool weaponFiltered)
        {
            if(obj is Lighter) {
                return 0;
            }
            return orig(self, obj, weaponFiltered);
        }

        private void GameSession_ctor(On.GameSession.orig_ctor orig, GameSession self, RainWorldGame game)
        {
            orig(self, game);
            CustomAtlases.FetchAtlas("Slimes");
            CustomAtlases.FetchAtlas("Shrines");
        }

        private void Player_SlugcatGrab(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            orig(self, obj, graspUsed);
            if (IsMe(self) && ((self.room.game.session is ArenaGameSession g && g.ScoreOfPlayer(self, false) < 6) || (self.room.game.IsStorySession && self.FoodInStomach != self.MaxFoodInStomach))) {
                if (obj is IPlayerEdible icr && !(obj is KarmaFlower) && !(obj is Mushroom) && (!(obj is Creature) || (obj is Creature cr && (cr.dead || cr is Fly || cr is SmallNeedleWorm || (cr is Centipede c && c.Edible))))) {
                    obj.slatedForDeletetion = true;
                    self.AddFood(icr.FoodPoints);
                    self.room.PlaySound(SoundID.Slime_Mold_Terrain_Impact, obj.firstChunk.pos, 1f, 1.2f);
                }
            }
        }

        private int Player_Grabability(On.Player.orig_Grabability orig, Player self, PhysicalObject obj)
        {
            if (obj is Lighter l && Lighter.getMine(self) != l) {
                return 0;
            }
            return orig(self, obj);
        }

        private bool Player_CanBeSwallowed(On.Player.orig_CanBeSwallowed orig, Player self, PhysicalObject testObj)
        {
            if(testObj is Lighter) {
                return false;
            }
            return orig(self, testObj);
        }

        bool releaseInterrupt(Player p, PhysicalObject l)
        {
            if (IsMe(p) && l is Lighter lig && (lig.lit)) {
                lig.lit = false;
                return true;
            }
            return false;
        }

        private void Player_ReleaseGrasp(On.Player.orig_ReleaseGrasp orig, Player self, int grasp)
        {
            if (!(self.grasps[grasp]?.grabbed is Lighter lig && releaseInterrupt(self, lig))) orig(self, grasp);
        }

        //private void IL_Player_ThrowObject(MonoMod.Cil.ILContext il)
        //{
        //    ILCursor baba = new ILCursor(il);
        //    ILCursor keke = new ILCursor(il);

        //    baba.GotoNext(
        //        x => x.MatchLdarg(0),
        //        x => x.MatchLdarg(1),
        //        x => x.MatchCallOrCallvirt<Player>("ReleaseGrasp"));
        //    Debug.Log("baba move");

        //    baba.Emit(OpCodes.Ldarg_0);
        //    baba.Emit(OpCodes.Ldarg_1);
        //    Debug.Log("baba emit ldargs");
        //    baba.EmitDelegate<Func<Player, int, bool>>((ply, grasp) => {
        //        var obj = ply.grasps[grasp].grabbed;
        //        return releaseInterrupt(ply, obj);
        //    });
        //    Debug.Log("baba emit delegate");

        //    ILLabel release = keke.DefineLabel();
        //    keke.GotoNext(x => x.MatchCallOrCallvirt<Player>("ReleaseGrasp"));
        //    Debug.Log("keke move");
        //    release = keke.MarkLabel();

        //    baba.Emit(OpCodes.Brtrue, release);
        //    Debug.Log("baba emit brtrue");
        //}

        private void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
        {
            if(self is Player p && IsMe(p) && type == Creature.DamageType.Explosion) {
                orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, 0, 0);
            } else orig(self,source,directionAndMomentum,hitChunk,hitAppendage,type,damage,stunBonus);
        }

        private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            if (IsMe(self)) {
                self.bounce = 0.4f;
            }
        }
        private class LighterStick : AbstractPhysicalObject.AbstractObjectStick
        {
            public LighterStick(AbstractPhysicalObject A, AbstractPhysicalObject B) : base(A, B)
            {

            }

            public override string SaveToString(int roomIndex)
            {
                return string.Concat(new string[]
                {
                roomIndex.ToString(),
                "<stkA>gripGoob<stkA>",
                this.A.ID.ToString(),
                "<stkA>",
                this.B.ID.ToString()
                });

            }
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            if (IsMe(self)) {
                Lighter l = Lighter.getMine(self);
                if(self.animation == Player.AnimationIndex.BellySlide) {
                    self.animation = Player.AnimationIndex.Roll;
                }

                if (l == null) {
                    AbstractLighter abstractLighter = new AbstractLighter(self.room.world, null, self.abstractCreature.pos, self.room.world.game.GetNewID(), self);
                    abstractLighter.RealizeInRoom();

                    self.abstractCreature.stuckObjects.Add(new LighterStick(self.abstractPhysicalObject, abstractLighter));
                } else {

                    if(self.room.abstractRoom.name == "SB_L01") {
                        l.Destroy();
                    }

                    if (l != null && (!(self.grasps[0]?.grabbed == l || self.grasps[1]?.grabbed == l))) {
                        Vector2 playerLoc = self.firstChunk.pos;
                        Vector2 lighterLoc = l.firstChunk.pos;
                        float ropeLength = 40f;
                        float elasticity = 0.2f;
                        float dist = Vector2.Distance(self.bodyChunks[1].pos, l.firstChunk.pos);

                        if (dist < 20) {
                            elasticity = 0;
                        }
                        if (dist > ropeLength) {
                            dist -= ropeLength;
                            elasticity *= 1 + dist * 0.5f;
                        }
                        if (self.animation == Player.AnimationIndex.Roll && l.mode != Weapon.Mode.Thrown)
                            elasticity += 1 + elasticity * 2;

                        float ratio = self.bodyChunks[1].mass / (l.firstChunk.mass + self.bodyChunks[1].mass);
                        Vector2 motionDir = Custom.DirVec(playerLoc, lighterLoc);
                        self.firstChunk.pos += motionDir * (1f - ratio) * elasticity * 0.2f;
                        self.firstChunk.vel += motionDir * (1f - ratio) * elasticity * 0.2f;
                        motionDir = Custom.DirVec(lighterLoc, playerLoc);
                        l.firstChunk.pos += motionDir * ratio * elasticity;
                        l.firstChunk.vel += motionDir * ratio * elasticity;
                    } //  /\ floobert shit /\

                    if (self.animation == Player.AnimationIndex.Roll && l.mode != Weapon.Mode.Thrown && Vector2.Distance(l.firstChunk.pos, self.firstChunk.pos) < 20) {
                        bool q = false;
                        if (self.grasps[0]?.grabbed == null && self.grasps[1]?.grabbed != l) { self.Grab(l, 0, 0, Creature.Grasp.Shareability.CanNotShare, 1, true, false); q = true; } else if (self.grasps[1]?.grabbed == null && self.grasps[0]?.grabbed != l) { self.Grab(l, 1, 0, Creature.Grasp.Shareability.CanNotShare, 1, true, false); q = true; }

                        if (q && (self.room.game.IsArenaSession || (self.room.game.session is StoryGameSession sgs && sgs.saveState.deathPersistentSaveData.theMark))) { l.timeSinceClick = 20; l.activateBladeMode(); }
                    }

                    if (self.animation == Player.AnimationIndex.RocketJump) {
                        if(l.bladeMode)
                            l.room.PlaySound(SoundID.Spear_Bounce_Off_Wall, l.firstChunk, false, 1f, .8f);
                        l.bladeMode = false;
                    }

                    if (self.rollCounter >= 10 && self.animation == Player.AnimationIndex.Roll) { 
                        if (l.rollRep >= 2) {
                            l.rollRep = 0;
                        } else {
                            l.rollRep++;
                            self.rollCounter--;
                        }
                    }
                }
                { // \/ food size shit \/
                    float foodFactor;
                    if (!self.room.game.IsArenaSession)
                        foodFactor = Mathf.Lerp(0.5f, 1.0f, (float)(self.FoodInStomach) / (float)(self.MaxFoodInStomach) + .4f);
                    else
                        foodFactor = 1f;


                    self.bounce = .4f;
                    if (self.animation == Player.AnimationIndex.Roll) {
                        self.bodyChunks[0].rad = 14 * foodFactor;
                        self.bodyChunks[1].rad = 14 * foodFactor;
                        self.bodyChunkConnections[0].distance = 5 * (foodFactor + .3f);
                    } else if (self.animation == Player.AnimationIndex.GrapplingSwing || self.bodyMode == Player.BodyModeIndex.CorridorClimb) {
                        self.bounce = .1f;
                        self.bodyChunks[0].rad = 7 * foodFactor;
                        self.bodyChunks[1].rad = 7 * foodFactor;
                        self.bodyChunkConnections[0].distance = 25 * (foodFactor + .3f);
                    } else {
                        self.bodyChunks[0].rad = foodFactor * 9;
                        self.bodyChunks[1].rad = foodFactor * 8;
                        self.bodyChunkConnections[0].distance = 17 * foodFactor;
                    }

                    self.bodyChunks[0].mass = foodFactor * 0.4f;
                    self.bodyChunks[1].mass = foodFactor * 0.3f;
                }
            }
        }

        private void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            FContainer fucko = newContatiner;

            if (newContatiner == null)
                newContatiner = rCam.ReturnFContainer("Midground");

            if (IsMe(self.player)) {
                var myContainer = sLeaser.containers?.FirstOrDefault(x => x.data is string s && s == "slime");
                if(myContainer != null) {
                    newContatiner.AddChild(myContainer);
                }
            }

            orig(self, sLeaser, rCam, fucko);
        }

        private void PlayerGraphics_ctor(On.PlayerGraphics.orig_ctor orig, PlayerGraphics self, PhysicalObject ow)
        {
            orig(self, ow);

            if (IsMe(self.player)) {
                self.tail[0] = new TailSegment(self, 8f, 6f, null, 0.85f, 1f, 1f, true);
                self.tail[1] = new TailSegment(self, 8f, 10f, self.tail[0], 0.85f, 1f, 0.5f, true);
                self.tail[2] = new TailSegment(self, 5.5f, 10f, self.tail[1], 0.85f, 1f, 0.5f, true);
                self.tail[3] = new TailSegment(self, 2f, 10f, self.tail[2], 0.85f, 1f, 0.5f, true);
            }
        }

        private void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (IsMe(self.player)) {
                var outlineContainer = sLeaser.containers?.FirstOrDefault(x => x.data is string s && s == "slime");
                if (outlineContainer == null) return;

                if (!sLeaser.sprites[9].element.name.Contains(idstring(self.player.playerState.playerNumber)) && sLeaser.sprites[9].element.name != null && sLeaser.sprites[9].element.name != "")
                    sLeaser.sprites[9].SetElementByName(sLeaser.sprites[9].element.name + idstring(self.player.playerState.playerNumber));


                if (!sLeaser.sprites[5].element.name.Contains("van")) {
                    sLeaser.sprites[5].SetElementByName(sLeaser.sprites[5].element.name + "van");
                }

                if (!sLeaser.sprites[6].element.name.Contains("van")) {
                    sLeaser.sprites[6].SetElementByName(sLeaser.sprites[6].element.name + "van");
                }

                sLeaser.sprites[2].color = Color.white; //this sucks

                sLeaser.sprites[9].isVisible = true;
                sLeaser.sprites[9].color = SlugcatEyeColor(self.player.playerState.playerNumber) ?? Color.black;

                int i = 0;
                foreach (FSprite vSprite in sLeaser.sprites) {
                    FSprite mSprite = (FSprite)outlineContainer.GetChildAt(i);
                    if (i == 2 || i == 7 || i == 8 || i == 11 || i == 10 || i == 9) {
                        mSprite.isVisible = false;
                    } else {
                        mSprite.SetPosition(vSprite.GetPosition());
                        mSprite.SetAnchor(vSprite.GetAnchor());
                        mSprite.rotation = vSprite.rotation;
                        mSprite.anchorX = vSprite.anchorX;
                        mSprite.anchorY = vSprite.anchorY;
                        mSprite.scaleX = vSprite.scaleX;
                        mSprite.scaleY = vSprite.scaleY;

                        if(vSprite != null)
                        mSprite.SetElementByName(vSprite.element.name + "slime");
                    }
                    if (i == 6 || i == 5) { //arms
                        mSprite.isVisible = vSprite.isVisible;
                    }

                    i++;
                }
            }
        }

        private void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);

            if (IsMe(self.player)) {
                FContainer fContainer = new FContainer {
                    data = "slime"
                };

                int i = 0; foreach (FSprite vSprite in sLeaser.sprites) {
                    FSprite mSprite = new FSprite(vSprite.element);

                    mSprite.color = volatileColor(self.player, LorO.Outline);
                    mSprite.SetPosition(vSprite.GetPosition());
                    fContainer.AddChild(mSprite);
                    i++;
                }
                if (sLeaser.containers == null)
                    sLeaser.containers = new FContainer[1];
                else
                    Array.Resize(ref sLeaser.containers, sLeaser.containers.Length + 1);
                sLeaser.containers[sLeaser.containers.Length - 1] = fContainer;

                TriangleMesh tailMesh = new TriangleMesh("Outline" + idstring(self.player.playerState.playerNumber), (sLeaser.sprites[2] as TriangleMesh).triangles, true);
                for (int j = tailMesh.vertices.Length - 1; j >= 0; j--) {
                    float num = (float)(j / 2) / (float)(tailMesh.vertices.Length / 2);
                    Vector2 vector;
                    if (j % 2 == 0) {                                                                
                        vector = new Vector2(num, 0f);
                    } else if (j < tailMesh.vertices.Length - 1) {
                        vector = new Vector2(num, 1f);
                    } else {
                        vector = new Vector2(1f, 0f);
                    }
                    vector.x = Mathf.Lerp(tailMesh.element.uvBottomLeft.x, tailMesh.element.uvTopRight.x, vector.x);
                    vector.y = Mathf.Lerp(tailMesh.element.uvBottomLeft.y, tailMesh.element.uvTopRight.y, vector.y);
                    tailMesh.UVvertices[j] = vector;
                }
                sLeaser.sprites[2] = tailMesh;


                self.AddToContainer(sLeaser, rCam, null);
            }
        }

        public string idstring(int slugcatCharacter)
        {
            if (SlimeConsole.forcecat != -1) {
                slugcatCharacter = SlimeConsole.forcecat;
            }
            switch (slugcatCharacter) {
                case 0: return "slime";
                case 1: return "gup";
                case 2: return "king";
                case 3: return "cat";
                default:
                    return "slime";
            }
        }

        public override string StartRoom => !SlimeConsole.LM ? "GW_S06" : "LW_A12";

        public override string Description => "this cat is s";

        [Description("0 for the skin color, 1 for the face color")]
        public Color volatileColor(Player p, LorO lorO)
        {
            int slugcatCharacter = p.playerState.slugcatCharacter;
            if (SlimeConsole.forcecat != -1) {
                slugcatCharacter = SlimeConsole.forcecat;
            }

            if (lorO == LorO.Outline) {
                switch (slugcatCharacter) {
                    case 0: return new Color(.3f, .6f, .3f);
                    case 1: return new Color(1, .9f, .5f);
                    case 2: return new Color(0, .9f, 1);
                    case 3: return new Color(.6f, .6f, .5f);
                    default: return new Color(.3f, .6f, .3f);
                }
            } else { //Lighter
                switch (slugcatCharacter) {
                    case 0: return new Color(.1f, .3f, .1f);
                    case 1: return new Color(.1f, .05f, 0);
                    case 2: return new Color(1, 1, 0);
                    case 3: return new Color(.1f, .1f, .1f);
                    default: return new Color(.1f, .3f, .1f);
                }
            }
        }

        public enum LorO
        {
            Lighter,
            Outline
        }

        public override Color? SlugcatColor(int slugcatCharacter, Color baseColor)
        {
            if (SlimeConsole.forcecat != -1) {
                slugcatCharacter = SlimeConsole.forcecat;
            }
            switch (slugcatCharacter) {
                case 0: return new Color(.5f, .9f, .5f);
                case 1: return new Color(1, .75f, 0);
                case 2: return new Color(0, .7f, 1);
                case 3: return new Color(.4f, .4f, .4f);
                default: return new Color(.5f, .9f, .5f);
            }
        }

        public override Color? SlugcatEyeColor(int slugcatCharacter)
        {
            if (SlimeConsole.forcecat != -1) {
                slugcatCharacter = SlimeConsole.forcecat;
            }
            switch (slugcatCharacter) {
                case 0: return new Color(.2f, .5f, .2f);
                case 1: return new Color(1, .9f, .3f);
                case 2: return new Color(1, 1, 1);
                case 3: return new Color(.2f, .2f, .1f);
                default: return new Color(.2f, .5f, .2f);
            }
        }

        public override float? GetCycleLength()
        {
            return Mathf.Lerp(6, 8, UnityEngine.Random.value);
        }

        protected override void GetStats(SlugcatStats stats)
        {
            stats.throwingSkill = 0;
            stats.lungsFac = 0.2f;
            stats.runspeedFac *= 1f;
            stats.corridorClimbSpeedFac *= 1.3f;
        }

        public override void GetFoodMeter(out int maxFood, out int foodToSleep)
        {
            maxFood = 6;
            foodToSleep = 3;
        }

        public override Stream GetResource(params string[] path)
        {
            var patchedPath = new string[path.Length];
            for (int i = path.Length - 1; i > -1; i--) patchedPath[i] = path[i];
            //this is needed because there are two scenes that have most assets in common, you probably won't have to use it

            if (path[path.Length - 2] == "SelectMenuDisrupt" && path.Last() != "scene.json")
                patchedPath[path.Length - 2] = "SelectMenu";
            //join the path parts from new array to get resource name to request
            string oresname = "TheVolatile.graphics." + string.Join(".", patchedPath);
            //attempt getting the resource. If name is wrong, null is returned
            var tryret = Assembly.GetExecutingAssembly().GetManifestResourceStream(oresname);
            if (tryret != null) Console.WriteLine($"BUILDING SCENE FROM ER: {oresname}");
            //if tryret is null, it means my name conversion was wrong or that i just didn't have the requested thing, let slugbase deal with it
            return tryret ?? base.GetResource(path);
        }

        public override void StartNewGame(Room room)
        {
            base.StartNewGame(room);
            if(room.game.session is StoryGameSession sgs) {
                sgs.saveState.deathPersistentSaveData.karma = 2;
            }


            if (room.abstractRoom.name != StartRoom) return;
            if (SlimeConsole.LM) room.AddObject(new SlimeStart(room));
        }
    }
}
