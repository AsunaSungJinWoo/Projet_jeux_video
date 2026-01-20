using Microsoft.Xna.Framework;           //Importe les classes de base de MonoGame (Game, GameTime, Vector2, Rectangle, Color, etc.)
using Microsoft.Xna.Framework.Graphics;  //Importe tout ce qui concerne le graphisme (SpriteBatch, Texture2D, etc.)
using Microsoft.Xna.Framework.Input;     //Importe la gestion des entrées (clavier, souris)
using System.Collections.Generic;        //Permet d'utiliser les collections comme Dictionary, List, etc.
using System.IO;                         //Permet de lire et écrire des fichiers (StreamReader, File.Exists, etc.)
using System.Text;
using System.Windows.Forms;              //Pour afficher des boîtes de dialogue

namespace jeux;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // Dictionnaire pour stocker la map : position -> ID de tuile
    private Dictionary<Vector2, int> sol = new Dictionary<Vector2, int>();

    // Texture atlas (image contenant toutes les tuiles)
    private Texture2D textureAtlas;

    // Taille des tuiles
    private const int Taille_tuille = 64;
    private const int nb_tuille_par_ligne_du_Tileset = 10;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.IsFullScreen = true;  //plein ecran
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;     //largeaur ecran mit par defaut
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;   //longueur ecran mit par defaut

        Content.RootDirectory = "Content"; //fichier ressources
        IsMouseVisible = true;  //curseur souris visible
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()//appeler aux demarrage seulement
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);


        textureAtlas = Content.Load<Texture2D>("Dungeon_Tileset");

        // Charger la map depuis le fichier CSV 
        sol = LoadMap("../../../data/level1_sol.csv");

        // Si aucune map n'est chargée, affiche un message d'erreur et ferme le jeu
        if (sol.Count == 0)
        {
            // Afficher une boîte de dialogue d'erreur
            System.Windows.Forms.MessageBox.Show(
                "Erreur : une carte n'a pas été chargée",  // Message d'erreur
                "Erreur",                                   // Titre de la fenêtre
                System.Windows.Forms.MessageBoxButtons.OK, // Bouton OK seulement
                System.Windows.Forms.MessageBoxIcon.Error  // Icône d'erreur (croix rouge)
            );

            // Fermer le jeu
            Exit();
        }
    }

    /// Charge une map depuis un fichier CSV
    private Dictionary<Vector2, int> LoadMap(string filepath)
    {
        Dictionary<Vector2, int> result = new Dictionary<Vector2, int>();

        // Vérifier si le fichier existe
        if (!File.Exists(filepath))
        {
            System.Diagnostics.Debug.WriteLine($"Fichier non trouvé : {filepath}");
            return result;
        }

        try
        {
            // Ouvrir le fichier en lecture
            StreamReader reader = new StreamReader(filepath);
            int y = 0;
            string ligne;

            // Lire ligne par ligne
            while ((ligne = reader.ReadLine()) != null)
            {
                // Séparer par les virgules
                string[] items = ligne.Split(',');

                for (int x = 0; x < items.Length; x++)
                {
                    string item = items[x].Trim();//enleve espace 

                    // Ignorer les cases vides
                    if (string.IsNullOrEmpty(item))//si rien passe
                        continue;

                    // Convertir en nombre
                    if (int.TryParse(item, out int value))
                    {
                        // Stocker seulement si >= 0
                        if (value >= 0)
                        {
                            result[new Vector2(x, y)] = value;
                        }
                    }
                }
                y++;
            }

            reader.Close();
            System.Diagnostics.Debug.WriteLine($"Map chargée : {result.Count} tuiles");//affiche sur la console nb de tuile charger
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erreur de chargement : {ex.Message}");
        }

        return result;
    }


    protected override void Update(GameTime gameTime) //appeler 60fois par seconde
    {
        // Quitter avec Echap
        if (Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) //gere le echap
            Exit();

        base.Update(gameTime); //appele update
    }

    protected override void Draw(GameTime gameTime) //60 fois par seconde pour dessiner
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);//met lecran en bleue

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);//affiche lecran et fait du pixel par pixel pour eviter le flou

        // Dessiner toutes les tuiles de la map
        foreach (var item in sol)
        {
            // Calculer la position de destination à l'écran (en pixels)
            Rectangle destRect = new Rectangle(
                (int)(item.Key.X * Taille_tuille),  // Position X
                (int)(item.Key.Y * Taille_tuille),  // Position Y
                Taille_tuille,                       // Largeur
                Taille_tuille                        // Hauteur
            );

            // Calculer quelle partie de l'atlas utiliser
            int tuileId = item.Value;
            int srcX = tuileId % nb_tuille_par_ligne_du_Tileset;  // Colonne dans l'atlas
            int srcY = tuileId / nb_tuille_par_ligne_du_Tileset;  // Ligne dans l'atlas

            // Rectangle source dans l'atlas
            Rectangle srcRect = new Rectangle(
                srcX * 64,  // Position X dans l'atlas
                srcY * 64,  // Position Y dans l'atlas
                64,         // Largeur dans l'atlas
                64          // Hauteur dans l'atlas
            );

            // Dessiner la tuile
            _spriteBatch.Draw(textureAtlas, destRect, srcRect, Color.White);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}

/*
=============================================================================
IMPORTANT - AJOUTER UNE RÉFÉRENCE
=============================================================================

Pour que MessageBox fonctionne, vous devez ajouter une référence à votre projet :

1. Clic droit sur votre projet dans l'Explorateur de solutions
2. Sélectionner "Ajouter" -> "Référence..."
3. Dans la fenêtre qui s'ouvre :
   - Cochez "System.Windows.Forms"
4. Cliquez sur OK

OU modifiez votre fichier .csproj pour ajouter cette ligne dans <ItemGroup> :
<ItemGroup>
    <Reference Include="System.Windows.Forms" />
</ItemGroup>

=============================================================================
*/