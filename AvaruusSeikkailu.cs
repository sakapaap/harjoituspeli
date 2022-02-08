using System;
using System.Collections.Generic;
using Jypeli;
using Jypeli.Assets;
using Jypeli.Controls;
using Jypeli.Widgets;


/// @author  Sanna Paappanen
/// @version 5.12.2019
///
/// <summary>
/// Peli, jossa kerätään tähtiä ja väistellään Lego-ukkeleita avaruusaluksella.
/// </summary>
public class AvaruusSeikkailu : PhysicsGame
{
    private const double MASSA = 50;
    private const double NOPEUS = 7000;
    private const double R = 70;
    private const int RUUDUN_KOKO = 40;

    private readonly Vector ylos = new Vector(0, 7000);
    private readonly Vector alas = new Vector(0, -7000);
    private readonly Vector oikea = new Vector(7000, 0);

    private static PhysicsObject pelaaja;
    private static PhysicsObject putoaja;
    private IntMeter pisteLaskuri;
    private List<Vector> osumat = new List<Vector>();
    private static List<Vector> paikat;

    private readonly Image[] kuvat = new Image[] { LoadImage("alus"), LoadImage("tahti"), LoadImage("ukkeli") };

    private readonly SoundEffect tahtiAani = LoadSoundEffect("TahtiAani");
    private readonly SoundEffect ukkeliAani = LoadSoundEffect("UkkeliAani");


    public override void Begin()
    {
        Gravity = new Vector(0, -200);

        LuoKentta();
        LuoPistelaskuri();

        paikat = Paikat(25, Level.Left, Level.Top - 10, Level.Right, Level.Top);
        pelaaja = LisaaPelaaja();
        putoaja = LisaaPutoaja(this, paikat);

        LisaaNappaimet();

        Camera.Follow(pelaaja);
        Camera.ZoomFactor = 1.2;
        Camera.StayInLevel = true;

        Timer liikutusajastin = new Timer();
        liikutusajastin.Interval = 0.01;
        liikutusajastin.Timeout += LiikuOikealle;
        liikutusajastin.Start();
    }


    /// <summary>
    /// Luo kentän ja tähtien paikat.
    /// </summary>
    void LuoKentta()
    {
        TileMap kentta = TileMap.FromLevelAsset("kentta1");
        kentta.SetTileMethod('*', LisaaTahti);
        kentta.Execute(RUUDUN_KOKO, RUUDUN_KOKO);

        Level.Background.CreateStars();
        Level.CreateLeftBorder();
        Level.CreateTopBorder();
        Level.CreateBottomBorder();
        PhysicsObject maali = Level.CreateRightBorder();
        maali.Tag = "maali";
    }


    /// <summary>
    /// Pistelaskuri, joka laskee pisteitä osumista tähtiin ja ukkeleihin ja näyttää ne pistenäytöllä.
    /// </summary>
    private void LuoPistelaskuri()
    {
        pisteLaskuri = new IntMeter(0, int.MinValue, int.MaxValue);

        Label pisteNaytto = new Label()
        {
            X = Screen.Left + 60,
            Y = Screen.Top - 50,
            TextColor = Color.Black,
            Color = Color.White,
            Title = "Pisteet",  
        };
        pisteNaytto.BindTo(pisteLaskuri);
        Add(pisteNaytto);
    }


    /// <summary>
    /// Luo tähdet kenttään.
    /// </summary>
    /// <param name="paikka">tähtien sijainti</param>
    /// <param name="leveys">leveys</param>
    /// <param name="korkeus">korkeus</param>
    void LisaaTahti(Vector paikka, double leveys, double korkeus)
    {
        PhysicsObject tahti = PhysicsObject.CreateStaticObject(R, R);
        tahti.CollisionIgnoreGroup = 1;
        tahti.Image = kuvat[1];
        tahti.Position = paikka; 
        tahti.Tag = "tahti";
        Add(tahti);
    }


    /// <summary>
    /// Luo pelaajan ja törmäysten käsittelijät.
    /// </summary>
    /// <returns>avaruusalus</returns>
    private PhysicsObject LisaaPelaaja()
    {
        PhysicsObject pelaaja = new PhysicsObject(R, R)
        {
            CanRotate = false,
            Image = kuvat[0],
            Mass = MASSA,
            Position = new Vector(Level.Left, 50),
            Restitution = 0.95,
            Tag = "pelaaja",
        };
        Add(pelaaja);
        pelaaja.LinearDamping = 0.95;

        AddCollisionHandler(pelaaja, "putoaja", HuonoOsuma);
        AddCollisionHandler(pelaaja, "tahti", HyvaOsuma);
        AddCollisionHandler(pelaaja, "maali", SaavuMaaliin);

        return pelaaja;
    }
    

    /// <summary>
    /// Käsittelee törmäyksen ukkeliin, mistä menettää 5 pistettä.
    /// </summary>
    /// <param name="pelaaja">avaruusalus</param>
    /// <param name="putoaja">Lego-ukkeli</param>
    void HuonoOsuma(PhysicsObject pelaaja, PhysicsObject putoaja)
    {
        ukkeliAani.Play();
        pisteLaskuri.Value -= 5;
        putoaja.Destroy();
    }


    /// <summary>
    /// Käsittelee törmäyksen tähteen, mistä saa 10 pistettä.
    /// </summary>
    /// <param name="pelaaja">avaruusalus</param>
    /// <param name="tahti">tähti</param>
    void HyvaOsuma(PhysicsObject pelaaja, PhysicsObject tahti)
    {
        tahtiAani.Play();
        pisteLaskuri.Value += 10;
        osumat.Add(tahti.Position);
        tahti.Destroy();

    }


    /// <summary>
    /// Ilmoittaa pelin loppumisesta.
    /// </summary>
    /// <param name="pelaaja">avaruusalus</param>
    /// <param name="maali">pelin oikea reuna</param>
    void SaavuMaaliin(PhysicsObject pelaaja, PhysicsObject maali)
    {
        
        MessageDisplay.Add(String.Format("Maali! Kerättyjen tähtien keskipaikka on {0:0.00} (min -220, max 220).", KerattyjenKeskipaikka(osumat)));
        Gravity = Vector.Zero;
        StopAll();
        Keyboard.Disable(Key.Up);
        Keyboard.Disable(Key.Down);
        Keyboard.Disable(Key.Right);
    }


    /// <summary>
    /// Laskee kerättyjen tähtien vertikaalisesta sijainnista keskiarvon.
    /// </summary>
    /// <param name="osumat">lista kerättyjen tähtien vektoreista</param>
    /// <returns>keskimääräinen y-sijainti</returns>
    private static double KerattyjenKeskipaikka(List<Vector> osumat)
    {
        int maara = osumat.Count;
        if (maara == 0) return double.MinValue;
        double summaY = osumat[0].Y;

        for (int i = 1; i < osumat.Count; i++)
        {
            summaY += osumat[i].Y;
        }

        double keskipaikka = summaY / maara;
        return keskipaikka;
    }


    /// <summary>
    /// Luo sijainnit putoaville ukkeleille.
    /// </summary>
    /// <param name="n">ukkeleiden määrä</param>
    /// <param name="x1">putoamisalueen vasemman reunan koordinaatti</param>
    /// <param name="y1">putoamisalueen alareunan koordinaatti</param>
    /// <param name="x2">putoamisalueen oikean reunan koordinaatti</param>
    /// <param name="y2">putoamisalueen yläreunan koordinaatti</param>
    /// <returns>putoamispaikat</returns>
    private List<Vector> Paikat(int n, double x1, double y1, double x2, double y2)
    {
        List<Vector> paikat = new List<Vector>(n);
        for (int i = 0; i < n; i++)
        {
            double x = RandomGen.NextDouble(x1, x2);
            double y = RandomGen.NextDouble(y1, y2);
            paikat.Add(new Vector(x, y));
        }
        return paikat;
    }


    /// <summary>
    /// Lisää ukkelin määritettyihin sijainteihin.
    /// </summary>
    /// <param name="game">tämä peli</param>
    /// <param name="paikat">putoamispisteet</param>
    /// <returns>lego-ukkeli</returns>
    private PhysicsObject LisaaPutoaja(PhysicsGame game, List<Vector> paikat)
    {
        for (int i = 0; i < paikat.Count; i++)
        {
            LuoPutoaja(game, paikat[i]);
        }
        return putoaja;
    }


    /// <summary>
    /// Luo Lego-ukkelin.
    /// </summary>
    /// <param name="game">tämä peli</param>
    /// <param name="p">putoamispisteet</param>
    private void LuoPutoaja(PhysicsGame game, Vector p)
    {
        PhysicsObject putoaja = new PhysicsObject(0.9 * R, 0.9 * R, Shape.Circle)
        {
            CollisionIgnoreGroup = 1,
            Image = kuvat[2],
            LinearDamping = 0.50,
            Mass = 0.10 * MASSA,
            Position = p,
            Tag = "putoaja",
        };
        game.Add(putoaja, 1);
    }


    /// <summary>
    /// Liikuttaa avaruusalusta oikealle.
    /// </summary>
    private void LiikuOikealle()
    {
        pelaaja.Push(new Vector(NOPEUS, 0.0));
    }


    /// <summary>
    /// Liikuttaa alusta nopeasti tarvittavaan suuntaan.
    /// </summary>
    /// <param name="pelaaja">avaruusalus</param>
    /// <param name="suunta">suuntavektori</param>
    private void Liikuta(PhysicsObject pelaaja, Vector suunta)
    {
        pelaaja.Hit(suunta);
    }


    /// <summary>
    /// Ohjaa näppäinkomentoja.
    /// </summary>
    void LisaaNappaimet()
    {
        Keyboard.Listen(Key.F1, ButtonState.Pressed, ShowControlHelp, "Näytä ohjeet.");
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli.");
        Keyboard.Listen(Key.Up, ButtonState.Pressed, Liikuta, "Alus nousee.", pelaaja, ylos);
        Keyboard.Listen(Key.Down, ButtonState.Pressed, Liikuta, "Alus laskeutuu.", pelaaja, alas);
        Keyboard.Listen(Key.Right, ButtonState.Pressed, Liikuta, "Alus laskeutuu.", pelaaja, oikea);
    }
}
