using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using BEPUphysics;
using BEPUphysics.Entities;
using BEPUphysics.DataStructures;
using BEPUphysics.NarrowPhaseSystems.Pairs;
using BEPUphysics.Entities.Prefabs;
using BEPUphysics.Collidables;
using BEPUphysics.Collidables.MobileCollidables;
using BEPUphysics.MathExtensions;
using BEPUphysics.Vehicle;
using BEPUphysics.CollisionRuleManagement;
using BEPUphysics.BroadPhaseSystems;
using BEPUphysics.Constraints.TwoEntity.Motors;
using BEPUphysics.Constraints.SingleEntity;
using BEPUphysics.OtherSpaceStages;
using BEPUphysics.Constraints.TwoEntity.Joints;
using BEPUphysicsDrawer.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;



namespace TankGame
{
    public class Tank : Box
    {
        public Ray frontLeft {get; set;}
        public Ray frontRight {get; set;}
        public Ray rearLeft {get; set;}
        public Ray rearRight {get; set;}
        public float health {get; set;}
        public float cannonROF {get;set;}
        public bool primed { get; set; }
        public int lives { get; set; }
        public int camMode { get; set; }
        public bool dead { get; set; }
        public Vector3 homePos { get; set; }
        public bool camSwap { get; set; }
        public float gunROF { get; set; }
        public float gunCD { get; set; }
        public GamePadState padState { get; set; }
        public Game myGame { get; set; }
        public float hover { get; set; }
        public RevoluteAngularJoint stabilizer { get; set; }
        public float hoverSpringConst { get; set; }

        public Tank(Vector3 pos, Game g) : base(pos, 3, 2, 6, 5000)
        {
            frontLeft = new Ray(new Vector3(this.Position.X + 1.5f, this.Position.Y - 1.0f, this.Position.Z + 3f), Vector3.Down);
            frontRight = new Ray(new Vector3(this.Position.X - 1.5f, this.Position.Y - 1.0f, this.Position.Z + 3f), Vector3.Down);
            rearLeft = new Ray(new Vector3(this.Position.X + 1.5f, this.Position.Y - 1.0f, this.Position.Z - 3f), Vector3.Down);
            rearRight = new Ray(new Vector3(this.Position.X - 1.5f, this.Position.Y - 1.0f, this.Position.Z - 3f), Vector3.Down);
            homePos = pos;
            hover = 4;
            health = 1000;
            cannonROF = 3.0f;
            lives = 5;
            camMode = 1;
            dead = false;
            camSwap = false;
            gunROF = 100;
            gunCD = 0;
            myGame = g;
            stabilizer = new RevoluteAngularJoint();
            stabilizer.ConnectionA = this;
            stabilizer.SpringSettings.DampingConstant = 20000;
            hoverSpringConst = 300f;
            LinearDamping = 0.6f;
            AngularDamping = 0.65f;
        }

        //Used to keep projectiles/rays from this tank from colliding with itself
        bool selfFilter(BroadPhaseEntry entry)
        {
            return entry != CollisionInformation && (entry.CollisionRules.Personal <= CollisionRule.Normal);
        }

        public void Kill()
        {
            if (lives > 1)
            {
                health = 1000;
                cannonROF = 3.0f;
                lives--;
                Reset(homePos);
            }
            else
            {
                this.Position = new Vector3(0, -20, 0);
                this.BecomeKinematic();
                this.IsAffectedByGravity = false;
                this.dead = true;
            }
        }

        public void Reset(Vector3 pos)
        {
            Position = pos;
            this.LinearMomentum = new Vector3(0, 0, 0);
            this.AngularMomentum = new Vector3(0, 0, 0);
            this.Orientation = Quaternion.Identity;
        }

        public void Update(GameTime gameTime)
        {
            //check death conditions
            if ((health <= 0) || (Position.Y <= -20.0f))
                Kill();

            //If this tank is dead set its camera to the wide angle shot
            if (dead)
                camMode = 2;
            else if (padState.IsButtonDown(Buttons.Y))
                camSwap = true;
            else if (padState.IsButtonUp(Buttons.Y) && camSwap)
            {
                camSwap = false;
                camMode++;
                if (camMode > 2)
                    camMode = 0;
            }

            //Update the remaining reload time for this tank's minigun
            gunCD -= gameTime.ElapsedGameTime.Milliseconds;
            if (gunCD < 0)
                gunCD = 0;


            //Update positions of thrusters (springs)
            frontLeft = new Ray(Position + (OrientationMatrix.Forward * HalfLength) + (OrientationMatrix.Left * HalfWidth) + (OrientationMatrix.Down * HalfHeight), Vector3.Down);
            frontRight = new Ray(Position + (OrientationMatrix.Forward * HalfLength) + (OrientationMatrix.Right * HalfWidth) + (OrientationMatrix.Down * HalfHeight), Vector3.Down);
            rearLeft = new Ray(Position + (OrientationMatrix.Backward * HalfLength) + (OrientationMatrix.Left * HalfWidth) + (OrientationMatrix.Down * HalfHeight), Vector3.Down);
            rearRight = new Ray(Position + (OrientationMatrix.Backward * HalfLength) + (OrientationMatrix.Right * HalfWidth) + (OrientationMatrix.Down * HalfHeight), Vector3.Down);

            //Damp vertical velocity if the tank gets launched upward for some reason
            if (LinearVelocity.Y > 4)
                LinearVelocity = new Vector3(LinearVelocity.X, LinearVelocity.Y * 0.95f, LinearVelocity.Z);

            float force;

            //Modifier ranging from 100%-25% used to reduce effectiveness of this tank's systems
            //Right now just reduces the strength of the support thrusters
            float damageModifier = 0.25f + (0.75f * (health / 1000f));

            //Stabilizer is only in effect if at least one of the thrusters is supported
            stabilizer.IsActive = false;
            RayCastResult result;

            if (Space.RayCast(frontLeft, 5.0f, selfFilter, out result))
            {
                stabilizer.IsActive = true;
                if (result.HitData.T < hover)
                {
                    force = (hover - result.HitData.T) * hoverSpringConst * damageModifier;
                    ApplyImpulse(frontLeft.Position, Vector3.Up * force);
                }
            }
            if (Space.RayCast(frontRight, 5.0f, selfFilter, out result))
            {
                stabilizer.IsActive = true;
                if (result.HitData.T < hover)
                {
                    force = (hover - result.HitData.T) * hoverSpringConst * damageModifier;
                    ApplyImpulse(frontRight.Position, new Vector3(0, force, 0));
                }
            }
            if (Space.RayCast(rearLeft, 5.0f, selfFilter, out result))
            {
                stabilizer.IsActive = true;
                if (result.HitData.T < hover)
                {
                    force = (hover - result.HitData.T) * hoverSpringConst * damageModifier;
                    ApplyImpulse(rearLeft.Position, new Vector3(0, force, 0));
                }
            }
            if (Space.RayCast(rearRight, 5.0f, selfFilter, out result))
            {
                stabilizer.IsActive = true;
                if (result.HitData.T < hover)
                {
                    force = (hover - result.HitData.T) * hoverSpringConst * damageModifier;
                    ApplyImpulse(rearRight.Position, new Vector3(0, force, 0));
                }
            }

            Vector3 thrustLoc, thrust;

            if (padState.IsConnected)
            {
                if (padState.ThumbSticks.Left.Length() != 0.0f)
                {
                    Vector3 right = OrientationMatrix.Right;
                    Vector3 forward = OrientationMatrix.Forward;
                    right = right * padState.ThumbSticks.Left.X;
                    forward = forward * padState.ThumbSticks.Left.Y;
                    Vector3 move = right + forward;
                    move.Normalize();
                    move = move * 1500f;
                    ApplyImpulse(Position, move);
                }
                if (padState.ThumbSticks.Right.Length() != 0.0f)
                {
                    thrustLoc = Position + (OrientationMatrix.Forward * 30f);
                    thrust = OrientationMatrix.Left * (25f * padState.ThumbSticks.Right.X);
                    thrustLoc = thrustLoc - (OrientationMatrix.Forward * HalfLength * 30);
                    thrustLoc = thrustLoc - (OrientationMatrix.Left * HalfWidth * padState.ThumbSticks.Right.X);
                    ApplyImpulse(thrustLoc, thrust);
                    thrust = OrientationMatrix.Up * (25f * padState.ThumbSticks.Right.Y);
                    ApplyImpulse(thrustLoc, thrust);
                }
            }
        }

        public void Gun()
        {
            
        }
    }

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        // Game State Variables

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont spriteFont;

        Space space;

        public Camera Camera;
        public Camera Camera2;

        //public ChaseCamera chase;

        public Model CubeModel;
        public Model redBox;
        public Model blueBox;
        public Model greenBox;
        public Model orangeBox;
        public Model whiteBox;
        public Model brownBox;
        public Model TankModel;

        public SoundEffect gun;
        public SoundEffectInstance gunLoop;
        public SoundEffectInstance gunLoop2;

        public Viewport vOne;
        public Viewport vTwo;
        public Viewport vThree;
        public Viewport vFour;

        public ModelDrawer modelDrawer;

        public Texture2D rTex, bTex, wTex, brTex, oTex, gTex;

        public Explosion boom;

        public Tank tank;
        public Tank tank2;

        public Cylinder barrel;

        public ConvexHull dummy;

        public Vector3 tankPos;
        public Vector3 tank2Pos;

        //public SingleEntityAngularMotor stabilizer;

        public RevoluteAngularJoint joint;
        public RevoluteAngularJoint joint2;

        public float springConst = 300;

        public Box shell;
        public Box bullet;

        bool nuke = false;
        bool building = false;

        // 486
        // New variables used for networking

        // 0 == local only, 1 == client, 2 == host
        public int serverMode;

        // Listen for connection searches on port 11500, input on port 11501
        public UdpClient connectionHandler, inputListener, transmitter;
        // Transmission back to remotes is handled by udpclient in the RemotePlayer instance

        private LinkedList<RemotePlayer> remotes;

        public IPEndPoint server, connectionHandlerEP, inputListenerEP;


//#if XBOX360
        /// <summary>
        /// Contains the latest snapshot of the gamepad's input state.
        /// </summary>
        //public GamePadState GamePadState;
//#else
        /// <summary>
        /// Contains the latest snapshot of the keyboard's input state.
        /// </summary>
        public KeyboardState KeyboardState;
        /// <summary>
        /// Contains the latest snapshot of the mouse's input state.
        /// </summary>
        public MouseState MouseState;

        public GamePadState padState;
        public GamePadState padState2;
//#endif

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 1440;
            graphics.PreferredBackBufferHeight = 900;
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            // 486
            // Prompt for host / server / local only mode
            //Console.WriteLine("Choose a game mode.\n1: Host a network game.\n2: Join a network game.\n3: Play local only.\n");
            serverMode = 0;

            remotes = new LinkedList<RemotePlayer>();

            //while ((serverMode != 0) && (serverMode != 1) && (serverMode != 2))
            //{
            //    KeyboardState = Keyboard.GetState();
            //    if (KeyboardState.IsKeyDown(Keys.NumPad1))
            //        serverMode = 0;
            //    else if (KeyboardState.IsKeyDown(Keys.D2))
            //        serverMode = 1;
            //    else if (KeyboardState.IsKeyDown(Keys.D3))
            //        serverMode = 2;
            //}


            //Create the cameras.
            Camera = new Camera(this, new Vector3(0, 3, 40), 5);
            Camera2 = new Camera(this, new Vector3(0, 3, 60), 5);

            //Create the viewports for splitscreen.
            vOne = new Viewport();
            vOne.X = 0;
            vOne.Y = 0;
            vOne.Width = 1440;
            vOne.Height = 450;
            vOne.MinDepth = 0;
            vOne.MaxDepth = 1;

            vTwo = new Viewport();
            vTwo.X = 0;
            vTwo.Y = 450;
            vTwo.Width = 1440;
            vTwo.Height = 450;
            vTwo.MinDepth = 0;
            vTwo.MaxDepth = 1;

            //Build the physics space.
            space = new Space();
            space.ThreadManager.AddThread();
            space.ThreadManager.AddThread();
            space.ThreadManager.AddThread();
            space.ThreadManager.AddThread();

            //Chuck some extra threads at the physics engine.
            //It seems to have been the only significant performance bottleneck thus far.
            //for (int i = 0; i < 8; i++)
            //{
            //    space.ThreadManager.AddThread();
            //}

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            //Load up the models for objects.
            CubeModel = Content.Load<Model>("cube");
            redBox = Content.Load<Model>("testBox");
            blueBox = Content.Load<Model>("blueBox");
            brownBox = Content.Load<Model>("brownBox");
            whiteBox = Content.Load<Model>("whiteBox");
            orangeBox = Content.Load<Model>("orangeBox");
            greenBox = Content.Load<Model>("greenBox");
            TankModel = Content.Load<Model>("tank");    //tank model credited to Greg Hanes

            //Sound effect for minigun
            gun = Content.Load<SoundEffect>("minigunShort");
            gunLoop = gun.CreateInstance();
            gunLoop.IsLooped = true;
            gunLoop2 = gun.CreateInstance();
            gunLoop2.IsLooped = true;

            //Used for the text overlay.
            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);
            spriteFont = Content.Load<SpriteFont>("gameFont");

            // TODO: use this.Content to load your game content here

            EntityModel model;

            //This would load and draw a box for any entities not covered by their own draw methods.
            //Everything is being drawn separately for now, leaving this in just in case.
            //foreach (Entity e in space.Entities)
            //{
            //    Box box = e as Box;
            //    if (box != null) //This won't create any graphics for an entity that isn't a box since the model being used is a box.
            //    {

            //        Matrix scaling = Matrix.CreateScale(box.Width, box.Height, box.Length); //Since the cube model is 1x1x1, it needs to be scaled to match the size of each individual box.
            //        model = new EntityModel(e, CubeModel, scaling, this);
            //        //Add the drawable game component for this entity to the game.
            //        Components.Add(model);
            //        e.Tag = model; //set the object tag of this entity to the model so that it's easy to delete the graphics component later if the entity is removed.
            //    }
            //}

            //Default starting positions for the tanks.
            tankPos = new Vector3(-10, 5, 50);
            tank2Pos = new Vector3(10, 5, 50);

            //Construct the tanks.
            tank = new Tank(tankPos, this);
            tank2 = new Tank(tank2Pos, this);

            //Project: Remake tanks as convex hulls, barring performance issues
            //
            //ModelMeshCollection.Enumerator meshEnum;
            //meshEnum = TankModel.Meshes.GetEnumerator();
            //ModelMesh tankMesh = meshEnum.Current;
            //dummy = new ConvexHull(new Vector3(20, 5, 50), tankMesh.MeshParts., 3000);

            Box ground = new Box(new Vector3(0, -14, 0), 130, 30, 130);

            //Handle some settings for the stabilizer that keeps the tanks from flipping over easily.
            //Placed here instead of the tank constructor because the joint needs to know about the ground object.
            tank.stabilizer.ConnectionB = ground;
            tank.stabilizer.WorldFreeAxisA = tank.stabilizer.WorldFreeAxisB = Vector3.Up;
            tank2.stabilizer.ConnectionB = ground;
            tank2.stabilizer.WorldFreeAxisA = tank2.stabilizer.WorldFreeAxisB = Vector3.Up;


            space.Solver.Add(tank.stabilizer);
            space.Solver.Add(tank2.stabilizer);

            //Set the gravity
            space.ForceUpdater.Gravity = new Vector3(0, -9.81f, 0);

            space.Add(tank);
            space.Add(tank2);

            //Create a big red box. Lava bad.
            Box lava = new Box(new Vector3(0, -30, 0), 400, 10, 400);
            space.Add(lava);
            lava.CollisionInformation.Events.InitialCollisionDetected += lavaCollision;
            model = new EntityModel(lava, redBox, Matrix.CreateScale(lava.Width, lava.Height, lava.Length), this);
            Components.Add(model);
            lava.Tag = model;

            //Create models for the tanks.
            model = new EntityModel(tank, TankModel, Matrix.Identity * Matrix.CreateRotationY(3.14f), this);
            Components.Add(model);
            tank.Tag = model;
            model = new EntityModel(tank2, TankModel, Matrix.Identity * Matrix.CreateRotationY(3.14f), this);
            Components.Add(model);
            tank2.Tag = model;

            //Create the ground.
            space.Add(ground);
            model = new EntityModel(ground, brownBox, Matrix.CreateScale(ground.Width, ground.Height, ground.Length), this);
            Components.Add(model);
            ground.Tag = model;

            //WORK IN PROGRESS: GUN BARRELS
            //barrel = new Cylinder(tank.Position + (tank.OrientationMatrix.Forward * 8), 1.0f, 0.25f);
            //space.Add(barrel);
            //model = new EntityModel(barrel, greenBox, Matrix.Identity, this);
            //Components.Add(model);

            //barrel.CollisionInformation.CollisionRules.Personal = CollisionRule.NoBroadPhase;

            //Not actually resetting here, just drawing them for the first time.
            ResetTowers();
        }

        //Various utility methods placed here.
        #region Collision Handlers
        //This handler just deletes anything the entity hits.
        //Retooled from the BEPU demos to use for the lava at the bottom of the world.
        /// <summary>
        /// Used to handle a collision event triggered by an entity specified above.
        /// </summary>
        /// <param name="sender">Entity that had an event hooked.</param>
        /// <param name="other">Entity causing the event to be triggered.</param>
        /// <param name="pair">Collision pair between the two objects in the event.</param>
        void lavaCollision(EntityCollidable sender, Collidable other, CollidablePairHandler pair)
        {
            //This type of event can occur when an entity hits any other object which can be collided with.
            //They aren't always entities; for example, hitting a StaticMesh would trigger this.
            //Entities use EntityCollidables as collision proxies; see if the thing we hit is one.
            var otherEntityInformation = other as EntityCollidable;
            if (otherEntityInformation != null)
            {
                //We hit an entity! remove it.
                space.Remove(otherEntityInformation.Entity);
                //Remove the graphics too.
                Components.Remove((EntityModel)otherEntityInformation.Entity.Tag);
            }
        }

        //Handles the event when a cannon shot collides
        void cannonHit(EntityCollidable sender, Collidable other, CollidablePairHandler pair)
        {
            var otherEntityInformation = other as EntityCollidable;

            //Checks if the target is a tank, damages if so
            if (otherEntityInformation.Entity.Position == tank.Position)
                tank.health = tank.health - 100;
            if (otherEntityInformation.Entity.Position == tank2.Position)
                tank2.health = tank2.health - 100;

            if (otherEntityInformation != null)
            {
                //Deletes the shell and generates an explosion
                boom = new Explosion(sender.Entity.Position, 90000, 2, space);
                boom.Explode();
                if (space.Entities.Contains(sender.Entity))
                    space.Remove(sender.Entity);
                Components.Remove((EntityModel)sender.Entity.Tag);
            }
        }

        //Handles the event when a minigun round collides
        void bulletHit(EntityCollidable sender, Collidable other, CollidablePairHandler pair)
        {
            var otherEntityInformation = other as EntityCollidable;

            //Checks if the target is a tank, damages if so
            if (otherEntityInformation.Entity.Position == tank.Position)
                tank.health = tank.health - 10;
            if (otherEntityInformation.Entity.Position == tank2.Position)
                tank2.health = tank2.health - 10;

            if (otherEntityInformation != null)
            {
                //Deletes the projectile and causes an explosion; minigun bullets do not currently explode
                //boom = new Explosion(sender.Entity.Position, 90000, 2, space);
                //boom.Explode();
                if (space.Entities.Contains(sender.Entity))
                    space.Remove(sender.Entity);
                Components.Remove((EntityModel)sender.Entity.Tag);
            }
        }
        #endregion

        #region Weapons Fire
        //Fire a cannon shot from the given tank; will move to tank class when tank's learn to load their own content
        public void Cannon(Tank t)
        {
            //Prevent repeat fire
            t.primed = false;

            //Calculate the starting position
            Vector3 launch = t.Position;
            launch = launch + (t.OrientationMatrix.Forward * 3);

            //Create the physics object
            shell = new Box(launch, 0.25f, 0.25f, 0.5f, 1.0f);

            //Do not collide with the parent tank
            shell.CollisionInformation.CollisionRules.Specific.Add(t.CollisionInformation.CollisionRules, CollisionRule.NoBroadPhase);
            space.Add(shell);

            //Continuous update is used since the shell's might be travelling very fast
            shell.PositionUpdateMode = BEPUphysics.PositionUpdating.PositionUpdateMode.Continuous;

            //Fire!
            shell.LinearVelocity = t.OrientationMatrix.Forward * 200;

            //Use the collision handler for cannon shells
            shell.CollisionInformation.Events.InitialCollisionDetected += cannonHit;

            //Create the graphics object
            Matrix scaling = Matrix.CreateScale(shell.Width, shell.Height, shell.Length);
            EntityModel model = new EntityModel(shell, redBox, scaling, this);
            //Add the drawable game component for this entity to the game.
            Components.Add(model);
            shell.Tag = model;
        }

        //Fires the given tank's minigun; will move to tank class when tank's learn to load their own content
        void Minigun(Tank t)
        {
            //Reset reload timer
            t.gunCD = t.gunROF;

            //Calculate launch position
            Vector3 launch = t.Position;
            launch = launch + (t.OrientationMatrix.Forward * 2.5f);

            //Create physics object
            bullet = new Box(launch, 0.1f, 0.1f, 0.1f, 0.1f);

            //Don't collide with the parent tank
            bullet.CollisionInformation.CollisionRules.Specific.Add(t.CollisionInformation.CollisionRules, CollisionRule.NoBroadPhase);

            //Using continuous collision in case of rapid muzzle velocity
            bullet.PositionUpdateMode = BEPUphysics.PositionUpdating.PositionUpdateMode.Continuous;
            space.Add(bullet);

            //Fire!
            bullet.LinearVelocity = t.OrientationMatrix.Forward * 300;

            //Use collision handler for minigun rounds
            bullet.CollisionInformation.Events.InitialCollisionDetected += bulletHit;

            //Create the graphics object
            Matrix scaling = Matrix.CreateScale(bullet.Width, bullet.Height, bullet.Length);
            EntityModel model = new EntityModel(bullet, redBox, scaling, this);
            //Add the drawable game component for this entity to the game.
            Components.Add(model);
            bullet.Tag = model;
        }
        #endregion

        #region Tower Controls
        //Spawn a tower
        public void Tower(Vector3 pos, float mass)
        {
            //Just recursively builds a tower of predetermined dimensions
            for (float x = pos.X; x < (pos.X + 4.0f); x = x + 1.0f)
            {
                for (float z = pos.Z; z < (pos.Z + 4.0f); z = z + 1.0f)
                {
                    for (float y = pos.Y; y < (pos.Y + 7.0f); y = y + 1.0f)
                    {
                        Box box = new Box(new Vector3(x, y, z), 1, 1, 1, mass);
                        space.Add(box);
                        Matrix scaling = Matrix.CreateScale(box.Width, box.Height, box.Length);
                        Model temp;
                        if (mass > 1000)
                            temp = greenBox;
                        else temp = orangeBox;

                        EntityModel model = new EntityModel(box, temp, scaling, this);
                        //Add the drawable game component for this entity to the game.
                        Components.Add(model);

                        box.Tag = model;
                    }
                }
            }
        }

        void ResetTowers()
        {
            //Builds a fixed set of towers
            Tower(new Vector3(10.0f, 1.5f, 10.0f), 9000.0f);
            Tower(new Vector3(18.0f, 1.5f, 5.0f), 9000.0f);
            Tower(new Vector3(37.0f, 1.5f, 56.0f), 9000.0f);
            Tower(new Vector3(-35.0f, 1.5f, 10.0f), 9000.0f);
            Tower(new Vector3(-60.0f, 1.5f, -30.0f), 9000.0f);
            Tower(new Vector3(-20.0f, 1.5f, -15.0f), 9000.0f);
            Tower(new Vector3(-30.0f, 1.5f, -30.0f), 9000.0f);

            Tower(new Vector3(-60.0f, 1.5f, 40.0f), 800.0f);
            Tower(new Vector3(40.0f, 1.5f, -20.0f), 800.0f);
            Tower(new Vector3(15.0f, 1.5f, 30.0f), 800.0f);
            Tower(new Vector3(27.0f, 1.5f, 15.0f), 800.0f);
            Tower(new Vector3(60.0f, 1.5f, 60.0f), 800.0f);
            Tower(new Vector3(60.0f, 1.5f, 40.0f), 800.0f);
            Tower(new Vector3(60.0f, 1.5f, -40.0f), 800.0f);
        }
        #endregion

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }


        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
//#if XBOX360
//            GamePadState = GamePad.GetState(0);
//#else
            KeyboardState = Keyboard.GetState();
            //MouseState = Mouse.GetState();
//#endif
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
//#if XBOX360
//                )
//#else
 || KeyboardState.IsKeyDown(Keys.Escape))
//#endif
                Exit();

            //Poll the gamepad(s)
            padState = GamePad.GetState(PlayerIndex.One);
            //padState2 = GamePad.GetState(PlayerIndex.One);
            padState2 = GamePad.GetState(PlayerIndex.Two);

            //Feed padStates to the tanks.
            tank2.padState = padState;
            //tank2.padState = padState2;

            //Run tank-specific update methods; check for death, apply spring forces, etc.
            tank.Update(gameTime);
            tank2.Update(gameTime);

            #region FIRE CONTROL
            //This can all move to the tank class once Cannon(), Minigun() etc move there
            //if (!padState.IsConnected && KeyboardState.IsKeyDown(Keys.Space))
            //    tank.primed = true;
            //if (padState.IsConnected && padState.Triggers.Left > 0.5f)
            //    tank.primed = true;
            //else if (((KeyboardState.IsKeyUp(Keys.Space) && !padState.IsConnected) || (padState.Triggers.Left < 0.5f && padState.IsConnected)) && tank.primed)
            //    Cannon(tank);

            if (KeyboardState.IsKeyDown(Keys.Space))
                tank.primed = true;
            else if (tank.primed)
                Cannon(tank);
            if (padState.IsConnected && padState.Triggers.Left > 0.5f)
                tank2.primed = true;
            else if (tank2.primed)
                Cannon(tank2);

            if (padState2.IsConnected && padState2.Triggers.Left > 0.5f)
                tank2.primed = true;
            else if ((padState2.Triggers.Left < 0.5f && padState2.IsConnected) && tank2.primed)
                Cannon(tank2);

            if ((padState.Triggers.Right > 0.99f) && (tank2.gunCD < 1))
                Minigun(tank2);

            //if (!padState.IsConnected && KeyboardState.IsKeyDown(Keys.Enter))
            //    Minigun(tank);

            if (KeyboardState.IsKeyDown(Keys.Enter))
                Minigun(tank);

            if ((padState2.Triggers.Right > 0.99f) && (tank2.gunCD < 1))
                Minigun(tank2);
            #endregion

            #region WORK IN PROGRESS: GUN BARRELS
            //Project: Add minigun barrels to the tank, update position relative to parent tank
            //Will be moved to tank.update() eventually
            //barrel.Position = tank.Position + (tank.OrientationMatrix.Forward * 8);
            //Matrix3X3 temp = barrel.OrientationMatrix;
            //temp.Forward = tank.OrientationMatrix.Forward;
            //barrel.OrientationMatrix = temp;
            #endregion

            #region Map Reset Controls
            //Rebuilds the default towers
            if (KeyboardState.IsKeyDown(Keys.PageDown))
                building = true;
            if (KeyboardState.IsKeyUp(Keys.PageDown) && building)
            {
                building = false;
                ResetTowers();
            }

            //NUKE THE SITE FROM ORBIT (clears map)
            if (KeyboardState.IsKeyDown(Keys.PageUp))
                nuke = true;
            if (KeyboardState.IsKeyUp(Keys.PageUp) && nuke)
            {
                nuke = false;
                boom = new Explosion(new Vector3(0, 3.0f, 0), 1000000000f, 1000f, space);
                boom.Explode();
            }
            #endregion


            #region NetworkControls

            // 486 Add Network Connection Controls
            // Yeah, it's an awful solution but it works for now.

            // Only do these checks if the network connection hasn't been established
            if (serverMode == -1)
            {
                if (KeyboardState.IsKeyDown(Keys.RightShift) && KeyboardState.IsKeyDown(Keys.D2))
                {
                    // Set up as a host
                    Console.WriteLine("I'm a server!\n");
                    serverMode = 2;
                    // Establish listening ports
                    connectionHandler = new UdpClient(11500);
                    inputListener = new UdpClient(11501);
                    // Create list for storing connected players
                    //remotes = new LinkedList<RemotePlayer>();
                }

                if (KeyboardState.IsKeyDown(Keys.RightShift) && KeyboardState.IsKeyDown(Keys.D1))
                {
                    // Set up connection to a server
                    Console.WriteLine("I'm a client!\n");
                    serverMode = 1;
                    connectionHandler = new UdpClient(11500);
                    connectionHandler.EnableBroadcast = true;
                    connectionHandler.Send(Encoding.ASCII.GetBytes("TankGameServerFind"),18,new IPEndPoint(IPAddress.Broadcast,11500));
                    string serverResponse = "";
                    IPEndPoint responder = new IPEndPoint(IPAddress.Any, 11500);
                    //int i = 0;
                    while (!serverResponse.Contains("TankGameServerResponse"))
                    {
                        //i++;
                        //Thread.Sleep(1000);
                        serverResponse = Encoding.ASCII.GetString(connectionHandler.Receive(ref responder));
                        //if (i == 6)
                        //{
                        //    // Failed to connect, reset serverMode to -1
                        //    serverMode = -1;
                        //}
                    }
                    server = responder;
                    transmitter = new UdpClient(server);
                }
            }

            #endregion

            #region NetworkUpdates

            // 486
            // If server:
            //  send updates to remotes
            //  read input from remotes
            // If client:
            //  listen for updates from server

            if (serverMode == 1)
            {
                // Do client stuff
                Byte[] sendBytes = Encoding.ASCII.GetBytes("Steve::Cannon");    // placeholder, just triggers the cannon fire repeatedly
                //int count = Encoding.ASCII.GetByteCount("Steve::Cannon");
                // send packet to the server via 'transmitter' which got bound to the IP that responded to our Server Search broadcast
                transmitter.Send(sendBytes,sendBytes.Length);
            }
            else if (serverMode == 2)
            {
                // Do server stuff
                connectionHandler.BeginReceive(HandleNewPlayer, new object());
                inputListener.BeginReceive(ReceiveClientUpdate, new Object());
            }

            #endregion

            #region DEBUGGING CONTROLS
            //DEBUGGING TOOLS
            //Key combo to pause using the breakpoint below.
            if (KeyboardState.IsKeyDown(Keys.RightShift) || KeyboardState.IsKeyDown(Keys.LeftShift))
                if (KeyboardState.IsKeyDown(Keys.P))
                {}

            //Reset position of tanks.
            if (KeyboardState.IsKeyDown(Keys.R))
                tank.Reset(tank.homePos);
            if (KeyboardState.IsKeyDown(Keys.T))
                tank2.Reset(tank2.homePos);

            //Reset position AND lives of tanks.
            if (KeyboardState.IsKeyDown(Keys.LeftShift))
                if (KeyboardState.IsKeyDown(Keys.R))
                {
                    tank.Reset(tank.homePos);
                    tank.lives = 5;
                    tank.dead = false;
                }
            if (KeyboardState.IsKeyDown(Keys.LeftShift))
                if (KeyboardState.IsKeyDown(Keys.T))
                {
                    tank2.Reset(tank2.homePos);
                    tank2.lives = 5;
                    tank.dead = false;
                }

            //Keyboard controls for tank 1.
            Vector3 thrustLoc, thrust;
            if (KeyboardState.IsKeyDown(Keys.I))
            {
                tank.ApplyImpulse(tank.Position, tank.OrientationMatrix.Forward * 1500f);
            }
            if (KeyboardState.IsKeyDown(Keys.K))
            {
                tank.ApplyImpulse(tank.Position, tank.OrientationMatrix.Backward * 1500f);
            }
            if (KeyboardState.IsKeyDown(Keys.J))
            {
                tank.ApplyImpulse(tank.Position, tank.OrientationMatrix.Left * 1500f);
            }
            if (KeyboardState.IsKeyDown(Keys.L))
            {
                tank.ApplyImpulse(tank.Position, tank.OrientationMatrix.Right * 1500f);
            }
            if (KeyboardState.IsKeyDown(Keys.U))
            {
                thrustLoc = tank.Position;
                thrustLoc = thrustLoc - (tank.OrientationMatrix.Forward * tank.HalfLength * 30);
                thrustLoc = thrustLoc - (tank.OrientationMatrix.Right * tank.HalfWidth);
                thrust = tank.OrientationMatrix.Right * 25f;
                tank.ApplyImpulse(thrustLoc, thrust);
            }
            if (KeyboardState.IsKeyDown(Keys.O))
            {
                thrustLoc = tank.Position;
                thrustLoc = thrustLoc - (tank.OrientationMatrix.Forward * tank.HalfLength * 30);
                thrustLoc = thrustLoc - (tank.OrientationMatrix.Left * tank.HalfWidth);
                thrust = tank.OrientationMatrix.Left * 25f;
                tank.ApplyImpulse(thrustLoc, thrust);
            }
#endregion

            #region AUDIO
            //Plays the minigun firing noise.
            //Will be moved to tank class when that class is reworked to load its own content.
            if (padState.Triggers.Right >= 0.99f)
                gunLoop.Play();
            if (padState.Triggers.Right < 0.99f)
                gunLoop.Stop();
            if (padState2.Triggers.Right >= 0.99f)
                gunLoop2.Play();
            if (padState2.Triggers.Right < 0.99f)
                gunLoop2.Stop();
            #endregion

            // 486
            // Check for new messages from remote clients
            //  possible messages:
            //      routine input update; gamepad stick state, etc.
            //      fire control: shoot main turret, fire minigun burst
            //      specials: fire jump jets, etc.
            // read from the single 'server input receiver' udpclient



            // send out updates to each remote client
            // generate list to send then loop through remote clients passing data packet to each one

            //Steps the simulation forward one time step.
            space.Update();

            base.Update(gameTime);

        }

        #region Net Code

        // Callback functions for asynchronous UDP reception

        public void HandleNewPlayer(IAsyncResult result)
        {
            connectionHandlerEP = new IPEndPoint(IPAddress.Any, 11500);
            Byte[] recvBytes = connectionHandler.EndReceive(result, ref connectionHandlerEP);

            string recvd = Encoding.ASCII.GetString(recvBytes);

            if (recvd.Contains("PlayerRegister::"))
            {
                string[] registration = recvd.Split("::".ToCharArray());
                remotes.AddLast(new RemotePlayer(connectionHandlerEP.Address, connectionHandlerEP.Port, registration[8]));
                Console.WriteLine("Registered new player: " + registration[8]);
            }
        }

        public void ReceiveClientUpdate(IAsyncResult result)
        {
            inputListenerEP = new IPEndPoint(IPAddress.Any, 11501);
            Byte[] recvBytes = inputListener.EndReceive(result, ref inputListenerEP);

            string recvd = Encoding.ASCII.GetString(recvBytes);

            if (recvd.Contains("InputMessage::"))
            {
                string[] inputMsg = recvd.Split("::".ToCharArray());
                // handle input data
                
            }
        }

        #endregion

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);

            // TODO: Add your drawing code here

            #region Camera Updates + Splitscreen Code

            Viewport original = graphics.GraphicsDevice.Viewport;

            //Update the camera.
            Vector3 cameraPos = tank.Position + (tank.OrientationMatrix.Down * 0.5f);
            Vector3 cameraTarget = tank.Position + (tank.OrientationMatrix.Down * 0.5f) + tank.OrientationMatrix.Forward;


            //This code calculates the camera settings for each viewport
            //Kind've a mess now, will probably move the camera to the tank class at some point
            //This stuff can then go into a tank.cameraUpdate() method
            if (tank.camMode == 1)
            {
                Vector3 back = new Vector3(tank.OrientationMatrix.Backward.X, 0, tank.OrientationMatrix.Backward.Z);
                back.Normalize();
                back = back * 40;
                cameraPos = cameraPos + back + (Vector3.Up * 6);
                Camera.target = tank.Position;
            }
            else if (tank.camMode == 2)
            {
                cameraPos = new Vector3(100, 50, 100);
                cameraTarget = tank.Position + ((tank2.Position - tank.Position) * 0.5f);
            }

            Camera.target = cameraTarget;
            Camera.Position = cameraPos;
            Camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            graphics.GraphicsDevice.Viewport = vOne;

            base.Draw(gameTime);

            cameraPos = tank2.Position;
            cameraTarget = tank2.Position + tank2.OrientationMatrix.Forward;

            if (tank2.camMode == 0)
            {
                cameraPos = tank2.Position;
                cameraTarget = tank2.Position + tank2.OrientationMatrix.Forward;
            }
            else if (tank2.camMode == 1)
            {
                Vector3 back = new Vector3(tank2.OrientationMatrix.Backward.X, 0, tank2.OrientationMatrix.Backward.Z);
                back.Normalize();
                back = back * 40;
                cameraPos = tank2.Position + back + (Vector3.Up * 6);
                cameraTarget = tank2.Position;
            }
            else if (tank2.camMode == 2)
            {
                cameraPos = new Vector3(100, 50, 100);
                cameraTarget = tank.Position + ((tank2.Position - tank.Position) * 0.5f);
            }


            //Camera.Yaw = Vector3.
            Camera.target = cameraTarget;
            Camera.Position = cameraPos;
            Camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds);


            graphics.GraphicsDevice.Viewport = vTwo;
            base.Draw(gameTime);

            GraphicsDevice.Viewport = original;
            #endregion

            #region Text Overlay
            spriteBatch.Begin();

            string text;

            if (!tank.dead)
            {
                text = "Red Player\nLives: " + tank.lives + "\nHealth: " + tank.health + " CD: " + tank.gunCD;
                spriteBatch.DrawString(spriteFont, text, new Vector2(33, 33), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(32, 32), Color.Red);
            }
            else
            {
                text = "Red Tank Eliminated!";
                spriteBatch.DrawString(spriteFont, text, new Vector2(33, 33), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(32, 32), Color.Red);
            }
            text = "";
            if (tank.camMode == 0)
                text += "Red Tank First Person";
            else if (tank.camMode == 1)
                text += "Red Tank Chase Cam";
            else if (tank.camMode == 2)
                text += "Wide Camera";
            else if (tank.camMode == 3)
                text += "Blue Tank First Person";
            else if (tank.camMode == 4)
                text += "Blue Tank Chase Cam";
            text += "\nLeft Stick: Movement\nRight Stick: Aim\nLeft Trigger: Cannon\nY: Change Camera";

            spriteBatch.DrawString(spriteFont, text, new Vector2(513, 9), Color.White);
            spriteBatch.DrawString(spriteFont, text, new Vector2(512, 8), Color.Green);

            if (!tank2.dead)
            {
                text = "Blue Player\nLives: " + tank2.lives + "\nHealth: " + tank2.health + " CD: " + tank2.gunCD;
                spriteBatch.DrawString(spriteFont, text, new Vector2(1025, 33), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(1024, 32), Color.Blue);
            }
            else
            {
                text = "Blue Tank Eliminated!";
                spriteBatch.DrawString(spriteFont, text, new Vector2(1025, 33), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(1024, 32), Color.Blue);
            }

            if (serverMode == 0)
            {
                text = "Network Mode: Local\nRemote Players: " + remotes.Count.ToString();
                spriteBatch.DrawString(spriteFont, text, new Vector2(768, 9), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(767, 8), Color.Orange);
            }
            else if (serverMode == 1)
            {
                text = "Network Mode: Client\nRemote Players: " + remotes.Count.ToString();
                spriteBatch.DrawString(spriteFont, text, new Vector2(768, 9), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(767, 8), Color.Orange);
            }
            else if (serverMode == 2)
            {
                text = "Network Mode: Host\nRemote Players: " + remotes.Count.ToString();
                spriteBatch.DrawString(spriteFont, text, new Vector2(768, 9), Color.White);
                spriteBatch.DrawString(spriteFont, text, new Vector2(767, 8), Color.Orange);
            }




            spriteBatch.End();

            //Repair the graphics device settings spriteBatch screws up
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            #endregion
        }
    }
}