using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.IO;
using System;

namespace aprentisage_tiled
{
    public class Projectile
    {
        public Vector2 Position;
        public Vector2 Direction;
        public float Speed = 400f;
        public bool IsActive = true;
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Dictionary<Vector2, int> collisions;
        private Dictionary<Vector2, int> sol;
        private Texture2D textureAtlas;
        private Texture2D playerTexture;
        private Texture2D projectileTexture;

        private Vector2 playerPosition;
        private float playerSpeed = 200f;
        private List<Projectile> projectiles = new List<Projectile>();

        private MouseState previousMouseState;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);

            // Mode plein écran sans bordure
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Activer le redimensionnement de la fenêtre
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
            Window.IsBorderless = true;

            sol = LoadMap("../../../data/level1_sol.csv", false);

            // Position initiale du joueur au centre
            playerPosition = new Vector2(960, 512);
        }

        private void OnResize(object sender, System.EventArgs e)
        {
            int newWidth = Window.ClientBounds.Width;
            int newHeight = Window.ClientBounds.Height;

            int maxHeight = 1080;
            if (newHeight > maxHeight)
            {
                newHeight = maxHeight;
            }

            _graphics.PreferredBackBufferWidth = newWidth;
            _graphics.PreferredBackBufferHeight = newHeight;
            _graphics.ApplyChanges();
        }

        private Dictionary<Vector2, int> LoadMap(string filepath, bool isCollisionLayer = false)
        {
            Dictionary<Vector2, int> result = new();
            StreamReader reader = new(filepath);
            int y = 0;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] items = line.Split(',');
                for (int x = 0; x < items.Length; x++)
                {
                    string item = items[x].Trim();

                    if (string.IsNullOrEmpty(item))
                        continue;

                    if (int.TryParse(item, out int value))
                    {
                        if (isCollisionLayer)
                        {
                            if (value == -1)
                                result[new Vector2(x, y)] = value;
                        }
                        else
                        {
                            if (value >= 0)
                                result[new Vector2(x, y)] = value;
                        }
                    }
                }
                y++;
            }
            reader.Close();
            return result;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            textureAtlas = Content.Load<Texture2D>("Dungeon_Tileset");

            // Créer des textures simples pour le joueur et les projectiles
            playerTexture = new Texture2D(GraphicsDevice, 32, 32);
            Color[] playerData = new Color[32 * 32];
            for (int i = 0; i < playerData.Length; i++)
                playerData[i] = Color.Green;
            playerTexture.SetData(playerData);

            projectileTexture = new Texture2D(GraphicsDevice, 8, 8);
            Color[] projectileData = new Color[8 * 8];
            for (int i = 0; i < projectileData.Length; i++)
                projectileData[i] = Color.Yellow;
            projectileTexture.SetData(projectileData);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            KeyboardState keyState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();

            // Déplacement du joueur avec les flèches
            Vector2 movement = Vector2.Zero;

            if (keyState.IsKeyDown(Keys.Up))
                movement.Y -= 1;
            if (keyState.IsKeyDown(Keys.Down))
                movement.Y += 1;
            if (keyState.IsKeyDown(Keys.Left))
                movement.X -= 1;
            if (keyState.IsKeyDown(Keys.Right))
                movement.X += 1;

            if (movement.Length() > 0)
            {
                movement.Normalize();
                playerPosition += movement * playerSpeed * deltaTime;

                // Limiter le joueur aux bordures de l'écran
                playerPosition.X = MathHelper.Clamp(playerPosition.X, 0, _graphics.PreferredBackBufferWidth - 32);
                playerPosition.Y = MathHelper.Clamp(playerPosition.Y, 0, _graphics.PreferredBackBufferHeight - 32);
            }

            // Tirer un projectile au clic gauche
            if (mouseState.LeftButton == ButtonState.Pressed &&
                previousMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
                Vector2 direction = mousePos - (playerPosition + new Vector2(16, 16));

                if (direction.Length() > 0)
                {
                    direction.Normalize();
                    projectiles.Add(new Projectile
                    {
                        Position = playerPosition + new Vector2(16, 16),
                        Direction = direction
                    });
                }
            }

            previousMouseState = mouseState;

            // Mettre à jour les projectiles
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                projectiles[i].Position += projectiles[i].Direction * projectiles[i].Speed * deltaTime;

                // Supprimer les projectiles hors écran
                if (projectiles[i].Position.X < 0 || projectiles[i].Position.X > _graphics.PreferredBackBufferWidth ||
                    projectiles[i].Position.Y < 0 || projectiles[i].Position.Y > _graphics.PreferredBackBufferHeight)
                {
                    projectiles.RemoveAt(i);
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int display_tilesize = 64;
            int num_tiles_per_row = 10;
            int pixel_tileseize = 64;

            // Dessiner la carte
            foreach (var item in sol)
            {
                Rectangle drect = new(
                    (int)(item.Key.X * display_tilesize),
                    (int)(item.Key.Y * display_tilesize),
                    display_tilesize,
                    display_tilesize
                );

                int x = item.Value % num_tiles_per_row;
                int y = item.Value / num_tiles_per_row;

                Rectangle src = new(
                    x * pixel_tileseize,
                    y * pixel_tileseize,
                    pixel_tileseize,
                    pixel_tileseize
                );

                _spriteBatch.Draw(textureAtlas, drect, src, Color.White);
            }

            // Dessiner les projectiles
            foreach (var projectile in projectiles)
            {
                _spriteBatch.Draw(projectileTexture,
                    new Rectangle((int)projectile.Position.X - 4, (int)projectile.Position.Y - 4, 8, 8),
                    Color.White);
            }

            // Dessiner le joueur
            _spriteBatch.Draw(playerTexture,
                new Rectangle((int)playerPosition.X, (int)playerPosition.Y, 32, 32),
                Color.White);

            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
