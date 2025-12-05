using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.IO;

namespace aprentisage_tiled
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Dictionary<Vector2, int> collisions;
        private Dictionary<Vector2, int> sol;
        private Texture2D textureAtlas;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 30 * 64; // 1920
            _graphics.PreferredBackBufferHeight = 20 * 64; // 1280
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            collisions = LoadMap("../../../data/level1_collisions.csv", true);
            sol = LoadMap("../../../data/level1_sol.csv", false);
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
                            // Pour les collisions, garde seulement les -1
                            if (value == -1)
                                result[new Vector2(x, y)] = value;
                        }
                        else
                        {
                            // Pour le sol, garde toutes les valeurs >= 0
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
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int display_tilesize = 64;
            int num_tiles_per_row = 10; // Ton atlas fait 640x640 = 10 tuiles par ligne
            int pixel_tileseize = 64;

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

            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
