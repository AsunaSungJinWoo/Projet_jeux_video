using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace jeux;

public class Exit
{
    public Rectangle Zone;
    public string Destination;
    public Vector2 SpawnPosition;
}

public class Game1 : Game
{
    // ==========================================================================
    // ÉTATS DU JEU
    // ==========================================================================
    private EtatJeu etatActuel = EtatJeu.Menu; // On commence au menu

    // ==========================================================================
    // MENU
    // ==========================================================================
    private int optionSelectionnee = 0;     // 0 = Jouer, 1 = Quitter
    private bool touchePrecedente = false; // Anti-répétition de touche
    private bool joueurEstMort = false; // Affiche "Vous etes mort" si vrai
    private SpriteFont fontMenu;             // Police d'écriture
    private Texture2D textureFond;          // Fond noir du menu

    private readonly Color COULEUR_SELECTIONNEE = Color.Yellow;
    private readonly Color COULEUR_NORMALE = Color.White;
    private readonly Color COULEUR_TITRE = Color.OrangeRed;
    private enum EtatJeu { Menu, EnJeu, MortRalentie }
    private float timerMort = 0f;
    private const float DUREE_MORT = 2.5f; // secondes avant retour au menu
    private Rectangle debugZone;
    // ============ COMPOSANTS GRAPHIQUES ============
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Dictionary<Vector2, int> sol = new Dictionary<Vector2, int>();
    private Dictionary<Vector2, int> decoration = new Dictionary<Vector2, int>();
    private HashSet<int> collisionTiles = new HashSet<int>();
    private List<Exit> exits = new List<Exit>();
    private Texture2D textureAtlas, playerTexture, meleeTexture, archerTexture, tankTexture;
    private Texture2D projectileTexture, whiteTexture, transitionTexture;
    private Dictionary<int, Texture2D> digitTextures = new Dictionary<int, Texture2D>();
    private const int TILE_SIZE = 64;
    private int tilesPerRow = 10;
    private const int PLAYER_SIZE = 64;
    private const float TRANSITION_DURATION = 1.0f;
    private Joueur joueur;
    private bool joueurInitialise = false;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private string nextMap = "";
    private Vector2 nextSpawn;
    private int mapWidth = 0, mapHeight = 0;
    private string currentMap = "level_1";
    private List<Enemy> enemies = new List<Enemy>();
    private List<Projectile> projectiles = new List<Projectile>();
    private Vector2 lastMoveDirection = new Vector2(0, 1);
    private KeyboardState previousKeyState;
    private MouseState previousMouseState;
    private Texture2D textureFondMenu;
    private const float ECHELLE_JOUEUR = 1.5f; // même valeur que dans animJoueur.Draw()
    // ============ ANIMATIONS ============
    private List<(Vector2 pos, float alpha)> dashAfterimages = new List<(Vector2, float)>();
    private float dashAfterimageTimer = 0f;
    private const float DASH_AFTERIMAGE_INTERVAL = 0.03f;
    private AnimationJoueur animJoueur;
    private Texture2D spritesheetJoueur;

    // ==========================================================================
    // CONSTRUCTEUR
    // ==========================================================================
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.IsFullScreen = true;
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
    }

    protected override void Initialize() { base.Initialize(); }

    // ==========================================================================
    // CLASSE JOUEUR
    // ==========================================================================
    public class Joueur
    {
        public Vector2 position;
        public int health = 5, maxHealth = 5, defence = 2, speed = 450, attack = 3, range = 2;
        public bool isAlive = true;
        public float attackCooldown = 0.2f, attackTimer = 0f, attackFlash = 0f;
        public float circularCooldown = 2.0f, circularTimer = 0f;
        public int circularAttack = 2;
        public float circularRange = 3.5f;
        public float dashCooldown = 1.5f, dashTimer = 0f;
        public bool isDashing = false;
        public float dashDuration = 0.15f, dashElapsed = 0f, dashSpeed = 1400f;
        public Vector2 dashDirection = Vector2.Zero;
        public float invincibleDuration = 1.0f, invincibleTimer = 0f;
        public bool IsInvincible => invincibleTimer > 0;

        public Joueur(Vector2 pos) { position = pos; }

        public void TakeDamage(int damage)
        {
            if (IsInvincible) return;
            int finalDamage = defence > 0 ? Math.Max(1, damage / defence) : damage;
            health -= finalDamage;
            if (health < 0) health = 0;
            if (health <= 0) isAlive = false;
            invincibleTimer = invincibleDuration;
        }

        public void Update(float deltaTime)
        {
            if (attackTimer < attackCooldown) attackTimer += deltaTime;
            if (circularTimer < circularCooldown) circularTimer += deltaTime;
            if (dashTimer < dashCooldown) dashTimer += deltaTime;
            if (invincibleTimer > 0) invincibleTimer -= deltaTime;
            if (attackFlash > 0) attackFlash -= deltaTime;
        }

        public bool TryMeleeAttack()
        {
            if (attackTimer >= attackCooldown) { attackTimer = 0f; attackFlash = 0.15f; return true; }
            return false;
        }

        public bool TryCircularAttack()
        {
            if (circularTimer >= circularCooldown) { circularTimer = 0f; return true; }
            return false;
        }

        public bool TryDash(Vector2 direction)
        {
            if (dashTimer >= dashCooldown && !isDashing && direction != Vector2.Zero)
            { dashTimer = 0f; isDashing = true; dashElapsed = 0f; dashDirection = direction; return true; }
            return false;
        }
    }

    // ==========================================================================
    // CLASSE ENNEMI
    // ==========================================================================
    public class Enemy
    {
        public Vector2 position;
        public int health, defence, speed, attack, range;
        public bool isAlive = true;
        public string type = "";
        public int tier = 1;
        public float attackCooldown = 1.0f, attackTimer = 0f, meleeFlashTimer = 0f;

        public void TakeDamage(int damage)
        {
            int finalDamage = defence > 0 ? Math.Max(1, damage / defence) : damage;
            health -= finalDamage;
            if (health <= 0) isAlive = false;
        }

        public bool IsInAttackRange(Vector2 joueurPos, int tileSize)
            => Vector2.Distance(position, joueurPos) <= range * tileSize;

        public bool TryAttack(float deltaTime, Vector2 joueurPos, int tileSize)
        {
            attackTimer += deltaTime;
            if (attackTimer >= attackCooldown && IsInAttackRange(joueurPos, tileSize))
            { attackTimer = 0f; return true; }
            return false;
        }
    }

    // ==========================================================================
    // CLASSE PROJECTILE
    // ==========================================================================
    public class Projectile
    {
        public Vector2 position, direction;
        public float speed = 500f;
        public int damage;
        public bool isAlive = true;
    }

    // ==========================================================================
    // CLASSES ENNEMIS SPÉCIALISÉES
    // ==========================================================================
    public class Melee_1 : Enemy { public Melee_1(Vector2 p) { position = p; health = 3; defence = 1; speed = 192; attack = 1; range = 2; type = "melee"; tier = 1; attackCooldown = 2.0f; } }
    public class Melee_2 : Enemy { public Melee_2(Vector2 p) { position = p; health = 3; defence = 1; speed = 192; attack = 2; range = 2; type = "melee"; tier = 2; attackCooldown = 2.0f; } }
    public class Melee_3 : Enemy { public Melee_3(Vector2 p) { position = p; health = 5; defence = 1; speed = 192; attack = 2; range = 2; type = "melee"; tier = 3; attackCooldown = 1.8f; } }
    public class Archer_1 : Enemy { public Archer_1(Vector2 p) { position = p; health = 3; defence = 1; speed = 220; attack = 1; range = 6; type = "archer"; tier = 1; attackCooldown = 2f; } }
    public class Archer_2 : Enemy { public Archer_2(Vector2 p) { position = p; health = 4; defence = 1; speed = 220; attack = 2; range = 8; type = "archer"; tier = 2; attackCooldown = 2f; } }
    public class Archer_3 : Enemy { public Archer_3(Vector2 p) { position = p; health = 4; defence = 2; speed = 256; attack = 2; range = 8; type = "archer"; tier = 3; attackCooldown = 2f; } }
    public class Tank_1 : Enemy { public Tank_1(Vector2 p) { position = p; health = 5; defence = 3; speed = 124; attack = 1; range = 1; type = "tank"; tier = 1; attackCooldown = 3.0f; } }
    public class Tank_2 : Enemy { public Tank_2(Vector2 p) { position = p; health = 8; defence = 3; speed = 124; attack = 1; range = 1; type = "tank"; tier = 2; attackCooldown = 3.0f; } }
    public class Tank_3 : Enemy { public Tank_3(Vector2 p) { position = p; health = 8; defence = 4; speed = 96; attack = 1; range = 1; type = "tank"; tier = 3; attackCooldown = 2.5f; } }

    // ==========================================================================
    // ATTAQUES DU JOUEUR
    // ==========================================================================
    private void PlayerMeleeAttack(Vector2 dir)
    {
        float tailleVisuelle = PLAYER_SIZE * ECHELLE_JOUEUR;
        Vector2 joueurCenter = joueur.position + new Vector2(tailleVisuelle / 2f, tailleVisuelle / 2f);

        if (!joueur.TryMeleeAttack()) return;
        if (dir == Vector2.Zero) dir = new Vector2(0, 1);
        dir.Normalize();



        // Centre de la zone = 1 tuile devant le joueur
        Vector2 zoneCentre = joueurCenter + dir * TILE_SIZE;

        int profondeur = TILE_SIZE * 2;     // 1 tuile dans la direction
        int largeurPerp = TILE_SIZE * 3; // 3 tuiles perpendiculaires

        // Si horizontal (droite/gauche) → largeur = profondeur, hauteur = 3 tuiles
        // Si vertical   (haut/bas)      → largeur = 3 tuiles,   hauteur = profondeur
        int zoneW = (int)(profondeur * Math.Abs(dir.X) + largeurPerp * Math.Abs(dir.Y));
        int zoneH = (int)(largeurPerp * Math.Abs(dir.X) + profondeur * Math.Abs(dir.Y));

        // Rectangle centré sur zoneCentre
        Rectangle zone = new Rectangle(
            (int)(zoneCentre.X - zoneW / 2f),
            (int)(zoneCentre.Y - zoneH / 2f),
            zoneW,
            zoneH
        );

        debugZone = zone; // ← debug, à supprimer après vérification

        foreach (var enemy in enemies)
        {
            if (!enemy.isAlive) continue;
            if (zone.Intersects(new Rectangle((int)enemy.position.X, (int)enemy.position.Y, PLAYER_SIZE, PLAYER_SIZE)))
                enemy.TakeDamage(joueur.attack);
        }
    }

    private void PlayerExplosionAttack(Vector2 dir)
    {
        if (!joueur.TryCircularAttack()) return;

        // Calcul du centre du joueur
        float tailleVisuelle = PLAYER_SIZE * ECHELLE_JOUEUR;
        Vector2 joueurCenter = joueur.position + new Vector2(tailleVisuelle / 2f, tailleVisuelle / 2f);

        // Direction par défaut si le joueur est immobile
        if (dir == Vector2.Zero) dir = new Vector2(0, 1);
        dir.Normalize();

        // Le point d'impact se situe à 3 tuiles devant le joueur
        Vector2 pointImpact = joueurCenter + (dir * (TILE_SIZE * 3));

        // Le rayon de l'explosion (3 tuiles)
        float rayonExplosion = TILE_SIZE * 3;

        // Mise à jour de la zone de debug pour visualiser l'explosion
        debugZone = new Rectangle(
            (int)(pointImpact.X - rayonExplosion),
            (int)(pointImpact.Y - rayonExplosion),
            (int)(rayonExplosion * 2),
            (int)(rayonExplosion * 2)
        );

        foreach (var enemy in enemies)
        {
            if (!enemy.isAlive) continue;

            Vector2 enemyCenter = enemy.position + new Vector2(PLAYER_SIZE / 2f, PLAYER_SIZE / 2f);

            // On vérifie si l'ennemi est dans le rayon de l'explosion
            if (Vector2.Distance(pointImpact, enemyCenter) <= rayonExplosion)
            {
                enemy.TakeDamage(joueur.circularAttack);
                // On peut ajouter un petit effet visuel ici plus tard
            }
        }
    }

    private void TryStartDash()
    {
        if (joueur.TryDash(lastMoveDirection))
        { joueur.invincibleTimer = joueur.dashDuration; dashAfterimages.Clear(); dashAfterimageTimer = 0f; }
    }

    private void UpdateDash(float deltaTime)
    {
        if (!joueur.isDashing) return;
        joueur.dashElapsed += deltaTime;
        Vector2 newPos = joueur.position + joueur.dashDirection * joueur.dashSpeed * deltaTime;
        if (!CheckPlayerCollision(newPos)) joueur.position = newPos;
        if (mapWidth > 0 && mapHeight > 0)
        {
            int tailleHitbox = (int)(PLAYER_SIZE * ECHELLE_JOUEUR);
            joueur.position.X = MathHelper.Clamp(joueur.position.X, 0, mapWidth - tailleHitbox);
            joueur.position.Y = MathHelper.Clamp(joueur.position.Y, 0, mapHeight - tailleHitbox);
        }
        if (joueur.dashElapsed >= joueur.dashDuration) joueur.isDashing = false;
        FadeAfterimages(deltaTime);
    }

    // ==========================================================================
    // GESTION DES ENNEMIS
    // ==========================================================================
    private void SpawnEnemy(string type, Vector2 position)
    {
        switch (type)
        {
            case "archer_1": enemies.Add(new Archer_1(position)); break;
            case "archer_2": enemies.Add(new Archer_2(position)); break;
            case "archer_3": enemies.Add(new Archer_3(position)); break;
            case "melee_1": enemies.Add(new Melee_1(position)); break;
            case "melee_2": enemies.Add(new Melee_2(position)); break;
            case "melee_3": enemies.Add(new Melee_3(position)); break;
            case "tank_1": enemies.Add(new Tank_1(position)); break;
            case "tank_2": enemies.Add(new Tank_2(position)); break;
            case "tank_3": enemies.Add(new Tank_3(position)); break;
            default: System.Diagnostics.Debug.WriteLine($"Type inconnu:{type}"); break;
        }
    }

    private void LoadEnemiesFromMap(XmlDocument doc)
    {
        foreach (XmlNode objGroup in doc.SelectNodes("//objectgroup"))
        {
            string groupName = objGroup.Attributes["name"]?.Value;
            if (groupName == "Enemie" || groupName == "enemie")
                foreach (XmlNode obj in objGroup.SelectNodes("object"))
                {
                    float x = float.Parse(obj.Attributes["x"]?.Value ?? "0");
                    float y = float.Parse(obj.Attributes["y"]?.Value ?? "0");
                    string enemyType = "";
                    foreach (XmlNode prop in obj.SelectNodes("properties/property"))
                        if (prop.Attributes["name"]?.Value == "classe")
                            enemyType = prop.Attributes["value"]?.Value;
                    if (!string.IsNullOrEmpty(enemyType))
                        SpawnEnemy(enemyType, new Vector2(x, y));
                }
        }
    }

    private void UpdateEnemy(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        foreach (var enemy in enemies)
        {
            if (!enemy.isAlive) continue;
            if (enemy.meleeFlashTimer > 0) enemy.meleeFlashTimer -= deltaTime;
            float distance = Vector2.Distance(enemy.position, joueur.position);
            float attackRangePixels = enemy.range * TILE_SIZE;
            float idealArcherRange = (enemy.range - 1) * TILE_SIZE;
            Vector2 direction = joueur.position - enemy.position;
            if (direction.Length() > 0) direction.Normalize();

            if (enemy.type == "archer")
            {
                if (distance > attackRangePixels)
                {
                    Vector2 np = enemy.position + direction * enemy.speed * deltaTime;
                    if (!CheckEnemyCollision(np)) enemy.position = np;
                    else { var nx = new Vector2(np.X, enemy.position.Y); if (!CheckEnemyCollision(nx)) enemy.position.X = nx.X; var ny = new Vector2(enemy.position.X, np.Y); if (!CheckEnemyCollision(ny)) enemy.position.Y = ny.Y; }
                }
                else if (distance < idealArcherRange)
                {
                    Vector2 np = enemy.position - direction * enemy.speed * deltaTime;
                    if (!CheckEnemyCollision(np)) enemy.position = np;
                    else { var nx = new Vector2(np.X, enemy.position.Y); if (!CheckEnemyCollision(nx)) enemy.position.X = nx.X; var ny = new Vector2(enemy.position.X, np.Y); if (!CheckEnemyCollision(ny)) enemy.position.Y = ny.Y; }
                }
            }
            else if (distance > PLAYER_SIZE)
            {
                Vector2 np = enemy.position + direction * enemy.speed * deltaTime;
                if (!CheckEnemyCollision(np)) enemy.position = np;
                else { var nx = new Vector2(np.X, enemy.position.Y); if (!CheckEnemyCollision(nx)) enemy.position.X = nx.X; var ny = new Vector2(enemy.position.X, np.Y); if (!CheckEnemyCollision(ny)) enemy.position.Y = ny.Y; }
            }

            if (enemy.TryAttack(deltaTime, joueur.position, TILE_SIZE))
            {
                if (enemy.type == "archer")
                {
                    Vector2 center = enemy.position + new Vector2(PLAYER_SIZE / 2f - 8, PLAYER_SIZE / 2f - 8);
                    Vector2 projDir = joueur.position - enemy.position;
                    if (projDir.Length() > 0) projDir.Normalize();
                    projectiles.Add(new Projectile { position = center, direction = projDir, damage = enemy.attack });
                }
                else { joueur.TakeDamage(enemy.attack); enemy.meleeFlashTimer = 0.2f; }
            }
        }
    }

    private void UpdateProjectiles(float deltaTime)
    {
        Rectangle playerRect = new Rectangle((int)joueur.position.X, (int)joueur.position.Y, PLAYER_SIZE, PLAYER_SIZE);
        foreach (var proj in projectiles)
        {
            if (!proj.isAlive) continue;
            proj.position += proj.direction * proj.speed * deltaTime;
            if (proj.position.X < -200 || proj.position.X > mapWidth + 200 || proj.position.Y < -200 || proj.position.Y > mapHeight + 200)
            { proj.isAlive = false; continue; }
            if (new Rectangle((int)proj.position.X, (int)proj.position.Y, 16, 16).Intersects(playerRect))
            { joueur.TakeDamage(proj.damage); proj.isAlive = false; }
        }
        projectiles.RemoveAll(p => !p.isAlive);
    }

    private void CleanDeadEnemies() => enemies.RemoveAll(e => !e.isAlive);
    private void CheckPlayerEnemyCollision() { }

    // ==========================================================================
    // HUD
    // ==========================================================================
    private void DrawHUD()
    {
        int heartSize = 28;  // un peu plus grands puisqu'ils sont seuls
        int heartStart = 20;  // démarre à gauche

        for (int i = 0; i < joueur.maxHealth; i++)
        {
            Color hc = i < joueur.health ? Color.Crimson : Color.DimGray;
            DrawHeart(heartStart + i * (heartSize + 6), 20, heartSize, hc);
        }

        // Cooldowns (repositionnés plus bas)
        DrawCooldownIcon(20, 70, "ATK", joueur.attackTimer, joueur.attackCooldown, Color.Cyan);
        DrawCooldownIcon(90, 70, "AOE", joueur.circularTimer, joueur.circularCooldown, Color.Magenta);
        DrawCooldownIcon(160, 70, "DASH", joueur.dashTimer, joueur.dashCooldown, Color.Yellow);
    }

    private void DrawCooldownIcon(int x, int y, string label, float current, float max, Color color)
    {
        int w = 60, h = 14, m = 2;
        _spriteBatch.Draw(whiteTexture, new Rectangle(x - m, y - m, w + m * 2, h + m * 2), Color.Black);
        _spriteBatch.Draw(whiteTexture, new Rectangle(x, y, w, h), Color.DarkGray);
        int fill = (int)(w * Math.Min(current / max, 1f));
        if (fill > 0) _spriteBatch.Draw(whiteTexture, new Rectangle(x, y, fill, h), color * (current >= max ? 1f : 0.5f));
    }

    private void DrawHeart(int x, int y, int size, Color color)
    {
        string[] heart = { "0110110", "1111111", "1111111", "0111110", "0011100", "0001000" };
        int px = Math.Max(1, size / 7);
        for (int row = 0; row < heart.Length; row++)
            for (int col = 0; col < heart[row].Length; col++)
                if (heart[row][col] == '1')
                    _spriteBatch.Draw(whiteTexture, new Rectangle(x + col * px, y + row * px, px, px), color);
    }

    private void DrawDashAfterimages()
    {
        foreach (var img in dashAfterimages)
            _spriteBatch.Draw(playerTexture,
                new Rectangle((int)img.pos.X, (int)img.pos.Y, PLAYER_SIZE, PLAYER_SIZE),
                Color.Cyan * img.alpha);
    }

    // ==========================================================================
    // CHARGEMENT DES RESSOURCES
    // ==========================================================================
    protected override void LoadContent()
    {
        spritesheetJoueur = Content.Load<Texture2D>("joueur_tileset"); // votre PNG
        animJoueur = new AnimationJoueur(spritesheetJoueur);

        _spriteBatch = new SpriteBatch(GraphicsDevice);
        textureFondMenu = Content.Load<Texture2D>("textureFondMenu");
        // Police pour le menu
        fontMenu = Content.Load<SpriteFont>("MenuFont");

        textureAtlas = Content.Load<Texture2D>("Dungeon_Tileset");
        playerTexture = CreateColorTexture(PLAYER_SIZE, PLAYER_SIZE, Color.Blue);
        meleeTexture = CreateColorTexture(PLAYER_SIZE, PLAYER_SIZE, Color.Red);
        archerTexture = CreateColorTexture(PLAYER_SIZE, PLAYER_SIZE, Color.Orange);
        tankTexture = CreateColorTexture(PLAYER_SIZE, PLAYER_SIZE, Color.Green);
        projectileTexture = CreateColorTexture(16, 16, Color.White);
        whiteTexture = CreateColorTexture(1, 1, Color.White);
        transitionTexture = CreateColorTexture(1, 1, Color.Black);
        textureFond = CreateColorTexture(1, 1, Color.Black);

        digitTextures[1] = CreateDigitTexture(1);
        digitTextures[2] = CreateDigitTexture(2);
        digitTextures[3] = CreateDigitTexture(3);

        joueur = new Joueur(new Vector2(
            GraphicsDevice.Viewport.Width / 2f - PLAYER_SIZE / 2f,
            GraphicsDevice.Viewport.Height / 2f - PLAYER_SIZE / 2f));
    }

    private Texture2D CreateColorTexture(int w, int h, Color color)
    {
        Texture2D tex = new Texture2D(GraphicsDevice, w, h);
        Color[] data = new Color[w * h];
        for (int i = 0; i < data.Length; i++) data[i] = color;
        tex.SetData(data); return tex;
    }

    private Texture2D CreateDigitTexture(int digit)
    {
        string[] p = digit switch
        {
            1 => new[] { "01000", "11000", "01000", "01000", "01000", "01000", "11100" },
            2 => new[] { "11100", "00010", "00010", "01100", "10000", "10000", "11110" },
            3 => new[] { "11110", "00010", "00010", "01110", "00010", "00010", "11110" },
            _ => new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" }
        };
        Texture2D tex = new Texture2D(GraphicsDevice, 5, 7);
        Color[] data = new Color[35];
        for (int r = 0; r < 7; r++) for (int c = 0; c < 5; c++)
                data[r * 5 + c] = p[r][c] == '1' ? Color.White : Color.Transparent;
        tex.SetData(data); return tex;
    }

    // ==========================================================================
    // DÉMARRER UNE NOUVELLE PARTIE
    // ==========================================================================
    private void DemarrerPartie()
    {
        joueurEstMort = false;
        joueurInitialise = false;
        currentMap = "level_1";

        animJoueur.Reinitialiser();

        // Remettre le joueur à neuf
        joueur = new Joueur(new Vector2(
            GraphicsDevice.Viewport.Width / 2f - PLAYER_SIZE / 2f,
            GraphicsDevice.Viewport.Height / 2f - PLAYER_SIZE / 2f));

        enemies.Clear();
        projectiles.Clear();
        dashAfterimages.Clear();

        LoadMap(currentMap, new Vector2(5, 5));
        etatActuel = EtatJeu.EnJeu; // ← Passer en jeu
    }

    // ==========================================================================
    // UPDATE
    // ==========================================================================
    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

        // ← Selon l'état, on fait des choses différentes
        switch (etatActuel)
        {
            case EtatJeu.Menu: UpdateMenu(); break;
            case EtatJeu.EnJeu: UpdateJeu(gameTime); break;
            case EtatJeu.MortRalentie: UpdateMortRalentie(gameTime); break; // ← ajouter
        }

        base.Update(gameTime);
    }

    // ==========================================================================
    // UPDATE MENU
    // ==========================================================================
    private void UpdateMenu()
    {
        KeyboardState clavier = Keyboard.GetState();

        bool bas = clavier.IsKeyDown(Keys.S) || clavier.IsKeyDown(Keys.Down);
        bool haut = clavier.IsKeyDown(Keys.Z) || clavier.IsKeyDown(Keys.Up);
        bool entree = clavier.IsKeyDown(Keys.Enter);

        if (bas && !touchePrecedente) optionSelectionnee = (optionSelectionnee + 1) % 2;
        else if (haut && !touchePrecedente) optionSelectionnee = (optionSelectionnee - 1 + 2) % 2;
        else if (entree && !touchePrecedente)
        {
            if (optionSelectionnee == 0) DemarrerPartie();
            else Exit();
        }

        touchePrecedente = bas || haut || entree;
    }

    // ==========================================================================
    // UPDATE JEU
    // ==========================================================================
    private void UpdateJeu(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        KeyboardState keyState = Keyboard.GetState();
        MouseState mouseState = Mouse.GetState();

        joueur.Update(dt);

        if (isTransitioning)
        {
            transitionTimer += dt;
            if (transitionTimer >= TRANSITION_DURATION / 2f && !string.IsNullOrEmpty(nextMap))
            { LoadMap(nextMap, nextSpawn); nextMap = ""; }
            if (transitionTimer >= TRANSITION_DURATION) { isTransitioning = false; transitionTimer = 0f; }
            previousKeyState = keyState; previousMouseState = mouseState;
            return;
        }

        Vector2 movement = Vector2.Zero;
        if (keyState.IsKeyDown(Keys.Z)) movement.Y -= 1;
        if (keyState.IsKeyDown(Keys.S)) movement.Y += 1;
        if (keyState.IsKeyDown(Keys.Q)) movement.X -= 1;
        if (keyState.IsKeyDown(Keys.D)) movement.X += 1;
        if (movement.Length() > 0) { movement.Normalize(); lastMoveDirection = movement; }

        bool shiftPressed = keyState.IsKeyDown(Keys.LeftShift) && previousKeyState.IsKeyUp(Keys.LeftShift);
        if (shiftPressed) TryStartDash();

        if (joueur.isDashing)
            UpdateDash(dt);
        else if (movement.Length() > 0)
        {
            Vector2 newPos = joueur.position + movement * joueur.speed * dt;
            if (!CheckPlayerCollision(newPos))
                joueur.position = newPos;
            else
            {
                var newPosX = new Vector2(newPos.X, joueur.position.Y);
                if (!CheckPlayerCollision(newPosX)) joueur.position.X = newPosX.X;
                var newPosY = new Vector2(joueur.position.X, newPos.Y);
                if (!CheckPlayerCollision(newPosY)) joueur.position.Y = newPosY.Y;
            }
            if (mapWidth > 0 && mapHeight > 0)
            {
                joueur.position.X = MathHelper.Clamp(joueur.position.X, 0, mapWidth - PLAYER_SIZE);
                joueur.position.Y = MathHelper.Clamp(joueur.position.Y, 0, mapHeight - PLAYER_SIZE);
            }
        }

        FadeAfterimages(dt);

        bool leftClick = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
        bool rightClick = mouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released;
        if (leftClick || keyState.IsKeyDown(Keys.Space)) PlayerMeleeAttack(lastMoveDirection);
        if (rightClick) PlayerExplosionAttack(lastMoveDirection);

        UpdateEnemy(gameTime);
        UpdateProjectiles(dt);
        CheckPlayerEnemyCollision();
        CleanDeadEnemies();
        CheckPlayerInExit();

        // ✅ Joueur mort → retour au menu
        if (!joueur.isAlive && etatActuel == EtatJeu.EnJeu)
        {
            animJoueur.Jouer("mort");
            timerMort = 0f;
            etatActuel = EtatJeu.MortRalentie;
        }

        previousKeyState = keyState;
        previousMouseState = mouseState;
        animJoueur.Update(dt);

        // Choisir l'animation selon le mouvement
        if (movement.Length() > 0)
        {
            if (movement.Y < 0) animJoueur.Jouer("marche_haut");
            else if (movement.Y > 0) animJoueur.Jouer("marche_bas");
            else if (movement.X < 0) animJoueur.Jouer("marche_gauche");
            else if (movement.X > 0) animJoueur.Jouer("marche_droite");
        }

        // Attaque mêlée
        if (leftClick)
        {
            if (lastMoveDirection.Y < 0) animJoueur.Jouer("attaque_haut");
            else if (lastMoveDirection.Y > 0) animJoueur.Jouer("attaque_bas");
            else if (lastMoveDirection.X < 0) animJoueur.Jouer("attaque_gauche");
            else animJoueur.Jouer("attaque_droite");
        }

        // Mort
        if (!joueur.isAlive) animJoueur.Jouer("mort");

    }

    // ==========================================================================
    // DRAW
    // ==========================================================================
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // ← Selon l'état, on dessine des choses différentes
        switch (etatActuel)
        {
            case EtatJeu.Menu: DrawMenu(); break;
            case EtatJeu.EnJeu: DrawJeu(gameTime); break;
            case EtatJeu.MortRalentie: DrawJeu(gameTime); break; 
        }

        base.Draw(gameTime);
    }

    // ==========================================================================
    // DRAW MENU
    // ==========================================================================
    private void DrawMenu()
    {
        int cx = GraphicsDevice.Viewport.Width / 2;
        int cy = GraphicsDevice.Viewport.Height / 2;

        _spriteBatch.Begin();

        // Fond noir
        _spriteBatch.Draw(textureFondMenu,
            new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
            Color.White);
        _spriteBatch.Draw(textureFond,
            new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
            Color.Black * 0.3f); // ← 0.5f = 50% opaque

        // Titre
        string titre = "Espcape the IUT";
        Vector2 szTitre = fontMenu.MeasureString(titre);
        _spriteBatch.DrawString(fontMenu, titre,
            new Vector2(cx - szTitre.X / 2, cy - 200), COULEUR_TITRE);

        // Message mort (visible seulement si le joueur est mort)
        if (joueurEstMort)
        {
            string msg = "Vous etes mort...";
            Vector2 szMsg = fontMenu.MeasureString(msg);
            _spriteBatch.DrawString(fontMenu, msg,
                new Vector2(cx - szMsg.X / 2, cy - 100), Color.Red);
        }

        // Option Jouer
        string lblJouer = optionSelectionnee == 0 ? "> Jouer" : "  Jouer";
        Vector2 szJouer = fontMenu.MeasureString(lblJouer);
        _spriteBatch.DrawString(fontMenu, lblJouer,
            new Vector2(cx - szJouer.X / 2, cy),
            optionSelectionnee == 1 ? COULEUR_SELECTIONNEE : COULEUR_NORMALE);

        // Option Quitter
        string lblQuitter = optionSelectionnee == 1 ? "> Quitter" : "  Quitter";
        Vector2 szQuitter = fontMenu.MeasureString(lblQuitter);
        _spriteBatch.DrawString(fontMenu, lblQuitter,
            new Vector2(cx - szQuitter.X / 2, cy + 80),
            optionSelectionnee == 1 ? COULEUR_SELECTIONNEE : COULEUR_NORMALE);

        // Instruction
        string instr = "Z/S naviguer   Entree confirmer";
        Vector2 szInstr = fontMenu.MeasureString(instr);
        _spriteBatch.DrawString(fontMenu, instr,
            new Vector2(cx - szInstr.X / 2, cy + 200), Color.White);

        _spriteBatch.End();
    }

    // ==========================================================================
    // DRAW JEU
    // ==========================================================================
    private void DrawJeu(GameTime gameTime)
    {
        if (!joueurInitialise)
        {
            joueur.position = new Vector2(
                GraphicsDevice.Viewport.Width / 2f - PLAYER_SIZE / 2f,
                GraphicsDevice.Viewport.Height / 2f - PLAYER_SIZE / 2f);
            joueurInitialise = true;
        }

        Matrix transformMatrix = Matrix.Identity;
        if (isTransitioning)
        {
            float progress = transitionTimer / TRANSITION_DURATION;
            float zoom = progress < 0.5f ? 1.0f - (progress * 2 * 0.9f) : 0.1f + ((progress - 0.5f) * 2 * 0.9f);
            Vector2 center = joueur.position + new Vector2(PLAYER_SIZE / 2f, PLAYER_SIZE / 2f);
            transformMatrix =
                Matrix.CreateTranslation(-center.X, -center.Y, 0) *
                Matrix.CreateScale(zoom) *
                Matrix.CreateTranslation(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2, 0);
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.End();

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transformMatrix);
        DrawLayer(sol);
        DrawLayer(decoration);
        DrawsEnemies();
        _spriteBatch.Draw(whiteTexture, debugZone, Color.Cyan * 0.3f);
        DrawProjectiles();
        DrawDashAfterimages();

        bool visible = !joueur.IsInvincible || ((int)(joueur.invincibleTimer * 10) % 2 == 0);
        if (visible)
            animJoueur.Draw(_spriteBatch, joueur.position, 1.5f);

        _spriteBatch.End();

        // HUD fixe
        _spriteBatch.Begin();
        DrawHUD();
        _spriteBatch.End();

        // Fondu au noir
        if (isTransitioning)
        {
            float progress = transitionTimer / TRANSITION_DURATION;
            float fade = progress < 0.5f ? progress * 2 : 1 - (progress - 0.5f) * 2;
            _spriteBatch.Begin();
            _spriteBatch.Draw(transitionTexture,
                new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
                Color.White * fade);
            _spriteBatch.End();
        }
    }

    // ==========================================================================
    // CHARGEMENT D'UNE CARTE
    // ==========================================================================
    private void LoadMap(string mapName, Vector2 spawnPosition)
    {
        currentMap = mapName;
        sol.Clear(); decoration.Clear(); collisionTiles.Clear();
        exits.Clear(); enemies.Clear(); projectiles.Clear();
        string tmxPath = $"../../../data/{mapName}.tmx";
        if (!File.Exists(tmxPath)) { System.Diagnostics.Debug.WriteLine($"ERREUR TMX:{tmxPath}"); return; }
        try
        {
            XmlDocument doc = new XmlDocument(); doc.Load(tmxPath);
            XmlNode mapNode = doc.SelectSingleNode("//map");
            if (mapNode != null)
            {
                mapWidth = int.Parse(mapNode.Attributes["width"]?.Value ?? "30") * TILE_SIZE;
                mapHeight = int.Parse(mapNode.Attributes["height"]?.Value ?? "16") * TILE_SIZE;
            }
            XmlNode tilesetNode = doc.SelectSingleNode("//tileset");
            if (tilesetNode != null)
            {
                string columns = tilesetNode.Attributes["columns"]?.Value;
                if (columns != null) tilesPerRow = int.Parse(columns);
                string tsxSource = tilesetNode.Attributes["source"]?.Value;
                string tsxPath = string.IsNullOrEmpty(tsxSource) ? "../../../Dungeon_Tileset.tsx"
                    : tsxSource.StartsWith("../") ? $"../../../{tsxSource.Substring(3)}"
                    : $"../../../data/{tsxSource}";
                LoadCollisionTilesFromTsx(tsxPath);
            }
            foreach (XmlNode layer in doc.SelectNodes("//layer"))
            {
                string ln = layer.Attributes["name"]?.Value;
                XmlNode dn = layer.SelectSingleNode("data"); if (dn == null) continue;
                Dictionary<Vector2, int> t = null;
                if (ln == "Sol" || ln == "sol") t = sol;
                else if (ln == "Decoration" || ln == "decoration") t = decoration;
                if (t != null) ParseLayerData(dn, t);
            }
            foreach (XmlNode og in doc.SelectNodes("//objectgroup"))
            {
                string gn = og.Attributes["name"]?.Value;
                if (gn == "Exits" || gn == "exits" || gn == "Exit" || gn == "exit")
                    foreach (XmlNode obj in og.SelectNodes("object")) ParseExitObject(obj);
            }
            LoadEnemiesFromMap(doc);
            int tailleHitbox = (int)(PLAYER_SIZE * ECHELLE_JOUEUR); // 96px

            Vector2 posBase = spawnPosition.X > 100 || spawnPosition.Y > 100
                ? spawnPosition
                : new Vector2(spawnPosition.X * TILE_SIZE, spawnPosition.Y * TILE_SIZE);

            // Décaler pour que le bas-milieu du sprite soit sur le point de spawn
            joueur.position = new Vector2(
                posBase.X - tailleHitbox / 2f,   // centrer horizontalement
                posBase.Y - tailleHitbox          // bas du sprite sur le point
            );
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ERREUR TMX:{ex.Message}"); }
    }

    private void ParseLayerData(XmlNode dataNode, Dictionary<Vector2, int> targetDict)
    {
        if (dataNode.Attributes["encoding"]?.Value != "csv") return;
        string[] rows = dataNode.InnerText.Trim().Split('\n');
        for (int y = 0; y < rows.Length; y++)
        {
            string[] tiles = rows[y].Split(',');
            for (int x = 0; x < tiles.Length; x++)
                if (int.TryParse(tiles[x].Trim(), out int tileId) && tileId > 0)
                    targetDict[new Vector2(x, y)] = tileId - 1;
        }
    }

    private void ParseExitObject(XmlNode obj)
    {
        try
        {
            float x = float.Parse(obj.Attributes["x"]?.Value ?? "0"), y = float.Parse(obj.Attributes["y"]?.Value ?? "0");
            float w = float.Parse(obj.Attributes["width"]?.Value ?? "64"), h = float.Parse(obj.Attributes["height"]?.Value ?? "64");
            Exit exit = new Exit { Zone = new Rectangle((int)x, (int)y, (int)w, (int)h) };
            XmlNode props = obj.SelectSingleNode("properties");
            if (props != null)
                foreach (XmlNode prop in props.SelectNodes("property"))
                {
                    string n = prop.Attributes["name"]?.Value, v = prop.Attributes["value"]?.Value;
                    if (n == "exit") exit.Destination = v;
                    else if (n == "coord_x") exit.SpawnPosition.X = float.Parse(v);
                    else if (n == "coord_y") exit.SpawnPosition.Y = float.Parse(v);
                }
            if (!string.IsNullOrEmpty(exit.Destination)) exits.Add(exit);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Erreur exit:{ex.Message}"); }
    }

    private void LoadCollisionTilesFromTsx(string filepath)
    {
        if (!File.Exists(filepath)) { System.Diagnostics.Debug.WriteLine($"TSX:{filepath}"); return; }
        try
        {
            XmlDocument doc = new XmlDocument(); doc.Load(filepath);
            foreach (XmlNode tile in doc.SelectNodes("//tile"))
            {
                if (!int.TryParse(tile.Attributes["id"]?.Value, out int tileId)) continue;
                foreach (XmlNode prop in tile.SelectNodes("properties/property"))
                    if (prop.Attributes["name"]?.Value == "type" && prop.Attributes["value"]?.Value == "collision")
                    { collisionTiles.Add(tileId); break; }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"TSX err:{ex.Message}"); }
    }

    // ==========================================================================
    // COLLISIONS
    // ==========================================================================
    private bool IsCollisionAt(Vector2 position)
    {
        Vector2 tp = new Vector2((int)(position.X / TILE_SIZE), (int)(position.Y / TILE_SIZE));
        return sol.ContainsKey(tp) && collisionTiles.Contains(sol[tp]);
    }

    private bool CheckPlayerCollision(Vector2 position)
    {
        int tailleHitbox = (int)(PLAYER_SIZE * ECHELLE_JOUEUR); // 64 × 1.5 = 96px ✅

        for (int i = 0; i <= 4; i++)
        {
            float o = (tailleHitbox - 1) * i / 4f;
            if (IsCollisionAt(new Vector2(position.X + o, position.Y))) return true;
            if (IsCollisionAt(new Vector2(position.X + o, position.Y + tailleHitbox - 1))) return true;
            if (IsCollisionAt(new Vector2(position.X, position.Y + o))) return true;
            if (IsCollisionAt(new Vector2(position.X + tailleHitbox - 1, position.Y + o))) return true;
        }
        return false;
    }

    private bool CheckEnemyCollision(Vector2 position)
    {
        for (int i = 0; i <= 4; i++)
        {
            float o = (PLAYER_SIZE - 1) * i / 4f;
            if (IsCollisionAt(new Vector2(position.X + o, position.Y))) return true;
            if (IsCollisionAt(new Vector2(position.X + o, position.Y + PLAYER_SIZE - 1))) return true;
            if (IsCollisionAt(new Vector2(position.X, position.Y + o))) return true;
            if (IsCollisionAt(new Vector2(position.X + PLAYER_SIZE - 1, position.Y + o))) return true;
        }
        return false;
    }

    private void CheckPlayerInExit()
    {
        if (isTransitioning) return;

        int tailleHitbox = (int)(PLAYER_SIZE * ECHELLE_JOUEUR); // 96px

        // ✅ Point bas-milieu du joueur
        int centreX = (int)(joueur.position.X + tailleHitbox / 2f);
        int basY = (int)(joueur.position.Y + tailleHitbox / 2f);

        // Petit rectangle centré sur le bas-milieu
        Rectangle pointJoueur = new Rectangle(
            centreX - 4,
            basY - 4,
            8,
            8
        );

        foreach (var exit in exits)
            if (pointJoueur.Intersects(exit.Zone))
            {
                isTransitioning = true;
                transitionTimer = 0f;
                nextMap = exit.Destination;
                nextSpawn = exit.SpawnPosition;
                return;
            }
    }
    private void FadeAfterimages(float deltaTime)
    {
        for (int i = dashAfterimages.Count - 1; i >= 0; i--)
        {
            var img = dashAfterimages[i];
            img.alpha -= deltaTime * 3f;
            if (img.alpha <= 0) dashAfterimages.RemoveAt(i);
            else dashAfterimages[i] = img;
        }
    }

    private void DrawLayer(Dictionary<Vector2, int> calque)
    {
        foreach (var item in calque)
        {
            int t = item.Value;
            _spriteBatch.Draw(textureAtlas,
                new Rectangle((int)(item.Key.X * TILE_SIZE), (int)(item.Key.Y * TILE_SIZE), TILE_SIZE, TILE_SIZE),
                new Rectangle((t % tilesPerRow) * 64, (t / tilesPerRow) * 64, 64, 64), Color.White);
        }
    }

    private void DrawsEnemies()
    {
        int ns = 3;
        foreach (var enemy in enemies)
        {
            if (!enemy.isAlive) continue;
            Texture2D tex = enemy.type switch { "melee" => "melee", "archer" => "archer", "tank" => "tank", _ => "melee" } switch
            { "melee" => meleeTexture, "archer" => archerTexture, "tank" => tankTexture, _ => meleeTexture };
            _spriteBatch.Draw(tex, new Rectangle((int)enemy.position.X, (int)enemy.position.Y, PLAYER_SIZE, PLAYER_SIZE), Color.White);
            if (enemy.meleeFlashTimer > 0)
                _spriteBatch.Draw(whiteTexture,
                    new Rectangle((int)enemy.position.X - 6, (int)enemy.position.Y - 6, PLAYER_SIZE + 12, PLAYER_SIZE + 12),
                    Color.White * 0.7f);
            if (digitTextures.ContainsKey(enemy.tier))
            {
                int nw = 5 * ns, nh = 7 * ns;
                _spriteBatch.Draw(digitTextures[enemy.tier],
                    new Rectangle((int)enemy.position.X + (PLAYER_SIZE - nw) / 2, (int)enemy.position.Y + (PLAYER_SIZE - nh) / 2, nw, nh),
                    Color.White);
            }
        }
    }

    private void DrawProjectiles()
    {
        foreach (var proj in projectiles)
            if (proj.isAlive)
                _spriteBatch.Draw(projectileTexture,
                    new Rectangle((int)proj.position.X, (int)proj.position.Y, 16, 16), Color.Yellow);
    }
    public class AnimationJoueur
    {
        // ============ CONSTANTES ============
        private const int FRAME_MARCHE = 64;  // taille d'une frame de marche/mort
        private const int FRAME_ATTAQUE = 192; // taille d'une frame d'attaque

        // ============ DÉFINITION DES ANIMATIONS ============
        // Chaque animation : (ligne dans le spritesheet, nombre de frames)
        private Dictionary<string, (int yPixels, int nbFrames, int tailleFrame)> animations
            = new Dictionary<string, (int, int, int)>
        {
            // Lignes de marche : 5 lignes × 64px
            { "marche_haut",    (0,   9, 64)  },  // Y = 0
            { "marche_gauche",  (64,  9, 64)  },  // Y = 64
            { "marche_bas",     (128, 9, 64)  },  // Y = 128
            { "marche_droite",  (192, 9, 64)  },  // Y = 192
            { "mort",           (256, 6, 64)  },  // Y = 256

            // Lignes d'attaque : commencent à Y=320 (après 5×64)
            { "attaque_haut",    (320, 6, 192) },  // Y = 320
            { "attaque_gauche",  (512, 6, 192) },  // Y = 512
            { "attaque_bas",     (704, 6, 192) },  // Y = 704
            { "attaque_droite",  (896, 6, 192) },  // Y = 896
        };

        // ============ ÉTAT ACTUEL ============
        private string animationActuelle = "marche_bas";
        private int frameActuelle = 0;
        private float timerFrame = 0f;
        private float vitesseMarche = 0.1f;  // secondes par frame
        private float vitesseAttaque = 0.08f;
        private bool animationTerminee = false; // pour mort et attaque

        private Texture2D spritesheet;

        public AnimationJoueur(Texture2D spritesheet)
        {
            this.spritesheet = spritesheet;
        }

        // ============ CHANGER D'ANIMATION ============
        public void Jouer(string nom)
        {
            // Ne pas interrompre une attaque ou la mort en cours
            if (animationActuelle == "mort") return;
            if (EstAttaque(animationActuelle) && !animationTerminee) return;

            // Ne pas redémarrer si déjà en cours
            if (animationActuelle == nom) return;

            animationActuelle = nom;
            frameActuelle = 0;
            timerFrame = 0f;
            animationTerminee = false;
        }

        // ============ MISE À JOUR ============
        public void Update(float deltaTime)
        {
            if (animationTerminee && animationActuelle != "mort") return;

            var (yPixels, nbFrames, tailleFrame) = animations[animationActuelle];
            float vitesse = EstAttaque(animationActuelle) ? vitesseAttaque : vitesseMarche;

            timerFrame += deltaTime;
            if (timerFrame >= vitesse)
            {
                timerFrame = 0f;
                frameActuelle++;

                // Fin de l'animation
                if (frameActuelle >= nbFrames)
                {
                    if (animationActuelle == "mort")
                        frameActuelle = nbFrames - 1; // rester sur la dernière frame
                    else if (EstAttaque(animationActuelle))
                    {
                        animationTerminee = true;      // attaque terminée
                        frameActuelle = 0;
                        // Revenir à l'idle correspondant
                        animationActuelle = animationActuelle.Replace("attaque", "marche");
                    }
                    else
                        frameActuelle = 0; // boucler (marche)
                }
            }
        }

        // ============ DESSIN ============
        public void Draw(SpriteBatch spriteBatch, Vector2 position, float echelle = 2f)
        {
            var (yPixels, nbFrames, tailleFrame) = animations[animationActuelle];

            Rectangle source = new Rectangle(
                frameActuelle * tailleFrame,
                yPixels,
                tailleFrame,
                tailleFrame
            );

            int tailleAffichee = (int)(tailleFrame * echelle); // ← taille × échelle

            Vector2 posDessin = tailleFrame == FRAME_ATTAQUE
                ? position - new Vector2((tailleAffichee - FRAME_MARCHE * echelle) / 2f)
                : position;

            Rectangle destination = new Rectangle(
                (int)posDessin.X,
                (int)posDessin.Y,
                tailleAffichee,  // ← largeur agrandie
                tailleAffichee   // ← hauteur agrandie
            );

            spriteBatch.Draw(spritesheet, destination, source, Color.White);
        }
        public void Reinitialiser()
        {
            animationActuelle = "marche_bas";
            frameActuelle = 0;
            timerFrame = 0f;
            animationTerminee = false;
        }
        private bool EstAttaque(string nom) => nom.StartsWith("attaque");
        public bool EstMort() => animationActuelle == "mort";
    }
    private void UpdateMortRalentie(GameTime gameTime)
    {
        // DeltaTime au ralenti (3× plus lent)
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds * 0.3f;

        timerMort += (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Continuer l'animation de mort au ralenti
        animJoueur.Update(dt);

        // Après la durée → retour au menu
        if (timerMort >= DUREE_MORT)
        {
            joueurEstMort = true;
            optionSelectionnee = 0;
            etatActuel = EtatJeu.Menu;
        }
    }
}
