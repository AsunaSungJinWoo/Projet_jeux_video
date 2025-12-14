using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.IO;
using System;

namespace aprentisage_tiled
{
    public class Enemy
    {
        public Vector2 Position;
        public int Health = 10;
        public bool IsAlive = true;
    }

    public class DamageText
    {
        public Vector2 Position;
        public int Damage;
        public float TimeLeft = 1.5f;
        public Color TextColor = Color.Red;
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Dictionary<Vector2, int> sol = new Dictionary<Vector2, int>();
        private HashSet<int> collisionTiles = new HashSet<int>();
        private Texture2D textureAtlas;
        private Texture2D playerTexture;
        private Texture2D debugTexture;
        private Texture2D heartTexture;
        private Texture2D enemyTexture;

        private Vector2 playerPosition;
        private float playerSpeed = 300f;
        private bool playerInitialized = false;
        private int playerHealth = 3;
        private int playerMaxHealth = 3;
        private int playerDamage = 2;

        private List<Enemy> enemies = new List<Enemy>();
        private List<DamageText> damageTexts = new List<DamageText>();

        private MouseState previousMouseState;
        private float attackCooldown = 0f;
        private float attackCooldownTime = 0.5f;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.IsFullScreen = true;
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            textureAtlas = Content.Load<Texture2D>("Dungeon_Tileset");

            // Charger la map avec chemin relatif
            sol = LoadMap("../../../data/level1_sol.csv");

            // Charger les collisions avec chemin relatif
            collisionTiles = LoadCollisionTilesFromTsx("../../../data/Dungeon_Tileset.tsx");

            // Charger l'image du coeur
            heartTexture = Content.Load<Texture2D>("coeur");

            // Créer le joueur
            playerTexture = new Texture2D(GraphicsDevice, 32, 32);
            Color[] playerData = new Color[32 * 32];
            for (int i = 0; i < playerData.Length; i++)
                playerData[i] = Color.Blue;
            playerTexture.SetData(playerData);

            // Créer texture debug (rouge très transparent)
            debugTexture = new Texture2D(GraphicsDevice, 64, 64);
            Color[] debugData = new Color[64 * 64];
            for (int i = 0; i < debugData.Length; i++)
                debugData[i] = new Color(255, 0, 0, 30);
            debugTexture.SetData(debugData);

            // Créer texture ennemi (carré rouge)
            enemyTexture = new Texture2D(GraphicsDevice, 32, 32);
            Color[] enemyData = new Color[32 * 32];
            for (int i = 0; i < enemyData.Length; i++)
                enemyData[i] = Color.Red;
            enemyTexture.SetData(enemyData);

            playerPosition = new Vector2(0, 0);

            // Créer quelques ennemis
            enemies.Add(new Enemy { Position = new Vector2(400, 300) });
            enemies.Add(new Enemy { Position = new Vector2(700, 400) });
            enemies.Add(new Enemy { Position = new Vector2(1200, 600) });
            enemies.Add(new Enemy { Position = new Vector2(500, 700) });
        }

        private Dictionary<Vector2, int> LoadMap(string filepath)
        {
            Dictionary<Vector2, int> result = new();

            if (!File.Exists(filepath))
            {
                return result;
            }

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
                        if (value >= 0)
                            result[new Vector2(x, y)] = value;
                    }
                }
                y++;
            }
            reader.Close();
            return result;
        }

        private HashSet<int> LoadCollisionTilesFromTsx(string filepath)
        {
            HashSet<int> collisions = new HashSet<int>();

            if (!File.Exists(filepath))
            {
                return collisions;
            }

            string content = File.ReadAllText(filepath);
            string[] lines = content.Split('\n');
            int? currentTileId = null;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("<tile id=\""))
                {
                    int startIdx = trimmed.IndexOf("id=\"") + 4;
                    int endIdx = trimmed.IndexOf("\"", startIdx);
                    if (int.TryParse(trimmed.Substring(startIdx, endIdx - startIdx), out int tileId))
                    {
                        currentTileId = tileId;
                    }
                }

                if (currentTileId.HasValue)
                {
                    if (trimmed.Contains("name=\"type\"") || trimmed.Contains("name=&quot;type&quot;"))
                    {
                        if (trimmed.Contains("value=\"collision") ||
                            trimmed.Contains("value=&quot;collision") ||
                            trimmed.Contains("collisions"))
                        {
                            collisions.Add(currentTileId.Value);
                            currentTileId = null;
                        }
                    }
                }

                if (trimmed.StartsWith("</tile>"))
                {
                    currentTileId = null;
                }
            }

            return collisions;
        }

        private bool IsCollisionAt(Vector2 position)
        {
            int tileX = (int)(position.X / 64);
            int tileY = (int)(position.Y / 64);
            Vector2 tilePos = new Vector2(tileX, tileY);

            if (sol.ContainsKey(tilePos))
            {
                return collisionTiles.Contains(sol[tilePos]);
            }
            return false;
        }

        private bool CheckPlayerCollision(Vector2 position)
        {
            int playerSize = 32;
            int checkPoints = 4;

            for (int i = 0; i <= checkPoints; i++)
            {
                float offset = (playerSize - 1) * i / (float)checkPoints;

                if (IsCollisionAt(new Vector2(position.X + offset, position.Y)))
                    return true;
                if (IsCollisionAt(new Vector2(position.X + offset, position.Y + playerSize - 1)))
                    return true;
                if (IsCollisionAt(new Vector2(position.X, position.Y + offset)))
                    return true;
                if (IsCollisionAt(new Vector2(position.X + playerSize - 1, position.Y + offset)))
                    return true;
            }

            return false;
        }

        private void PerformAttack()
        {
            if (attackCooldown > 0) return;

            // Zone d'attaque devant le joueur (50 pixels de portée)
            Rectangle attackZone = new Rectangle(
                (int)playerPosition.X - 25,
                (int)playerPosition.Y - 25,
                82,
                82
            );

            // Vérifier si un ennemi est dans la zone
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                Rectangle enemyRect = new Rectangle(
                    (int)enemy.Position.X,
                    (int)enemy.Position.Y,
                    32,
                    32
                );

                if (attackZone.Intersects(enemyRect))
                {
                    enemy.Health -= playerDamage;

                    // Afficher les dégâts
                    damageTexts.Add(new DamageText
                    {
                        Position = new Vector2(enemy.Position.X, enemy.Position.Y - 20),
                        Damage = playerDamage
                    });

                    if (enemy.Health <= 0)
                    {
                        enemy.IsAlive = false;
                    }
                }
            }

            attackCooldown = attackCooldownTime;
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            KeyboardState keyState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();

            // Réduire le cooldown d'attaque
            if (attackCooldown > 0)
                attackCooldown -= deltaTime;

            // Déplacement du joueur
            Vector2 movement = Vector2.Zero;

            if (keyState.IsKeyDown(Keys.Z))
                movement.Y -= 1;
            if (keyState.IsKeyDown(Keys.S))
                movement.Y += 1;
            if (keyState.IsKeyDown(Keys.Q))
                movement.X -= 1;
            if (keyState.IsKeyDown(Keys.D))
                movement.X += 1;

            if (movement.Length() > 0)
            {
                movement.Normalize();
                Vector2 newPosition = playerPosition + movement * playerSpeed * deltaTime;

                if (!CheckPlayerCollision(newPosition))
                {
                    playerPosition = newPosition;
                }
                else
                {
                    Vector2 newPosX = new Vector2(newPosition.X, playerPosition.Y);
                    if (!CheckPlayerCollision(newPosX))
                    {
                        playerPosition.X = newPosX.X;
                    }

                    Vector2 newPosY = new Vector2(playerPosition.X, newPosition.Y);
                    if (!CheckPlayerCollision(newPosY))
                    {
                        playerPosition.Y = newPosY.Y;
                    }
                }

                playerPosition.X = MathHelper.Clamp(playerPosition.X, 0, GraphicsDevice.Viewport.Width - 32);
                playerPosition.Y = MathHelper.Clamp(playerPosition.Y, 0, GraphicsDevice.Viewport.Height - 32);
            }

            // Attaque au clic gauche
            if (mouseState.LeftButton == ButtonState.Pressed &&
                previousMouseState.LeftButton == ButtonState.Released)
            {
                PerformAttack();
            }

            previousMouseState = mouseState;

            // Mettre à jour les textes de dégâts
            for (int i = damageTexts.Count - 1; i >= 0; i--)
            {
                damageTexts[i].TimeLeft -= deltaTime;
                damageTexts[i].Position.Y -= 30 * deltaTime; // Flotte vers le haut

                if (damageTexts[i].TimeLeft <= 0)
                {
                    damageTexts.RemoveAt(i);
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (!playerInitialized)
            {
                playerPosition = new Vector2(
                    GraphicsDevice.Viewport.Width / 2f - 16,
                    GraphicsDevice.Viewport.Height / 2f - 16
                );
                playerInitialized = true;
            }

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int tileSize = 64;
            int tilesPerRow = 10;

            // Dessiner la map
            foreach (var item in sol)
            {
                Rectangle destRect = new(
                    (int)(item.Key.X * tileSize),
                    (int)(item.Key.Y * tileSize),
                    tileSize,
                    tileSize
                );

                int srcX = item.Value % tilesPerRow;
                int srcY = item.Value / tilesPerRow;

                Rectangle srcRect = new(
                    srcX * 64,
                    srcY * 64,
                    64,
                    64
                );

                _spriteBatch.Draw(textureAtlas, destRect, srcRect, Color.White);

                if (collisionTiles.Contains(item.Value))
                {
                    _spriteBatch.Draw(debugTexture, destRect, Color.White);
                }
            }

            _spriteBatch.End();

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Dessiner les ennemis
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive)
                {
                    _spriteBatch.Draw(enemyTexture,
                        new Rectangle((int)enemy.Position.X, (int)enemy.Position.Y, 32, 32),
                        Color.White);
                }
            }

            // Dessiner le joueur
            _spriteBatch.Draw(playerTexture,
                new Rectangle((int)playerPosition.X, (int)playerPosition.Y, 32, 32),
                Color.White);

            _spriteBatch.End();

            // Dessiner l'UI (coeurs et dégâts)
            _spriteBatch.Begin();

            // Afficher les coeurs
            for (int i = 0; i < playerHealth; i++)
            {
                _spriteBatch.Draw(heartTexture,
                    new Rectangle(20 + i * 45, 20, 40, 40),
                    Color.White);
            }

            // Afficher les textes de dégâts
            foreach (var damageText in damageTexts)
            {
                DrawNumber(damageText.Damage, (int)damageText.Position.X, (int)damageText.Position.Y, damageText.TextColor);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // Méthode pour dessiner un nombre avec des carrés
        private void DrawNumber(int number, int x, int y, Color color)
        {
            string numStr = number.ToString();
            int digitWidth = 12;

            for (int i = 0; i < numStr.Length; i++)
            {
                Texture2D digitTexture = new Texture2D(GraphicsDevice, 10, 15);
                Color[] data = new Color[10 * 15];
                for (int j = 0; j < data.Length; j++)
                    data[j] = color;
                digitTexture.SetData(data);

                _spriteBatch.Draw(digitTexture,
                    new Rectangle(x + i * digitWidth, y, 10, 15),
                    Color.White);
            }
        }
    }
}
