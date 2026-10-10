using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Prototipin tüm sahne kurulumunu tek tıkla yapan editör menüsü.
/// Editöre elle hiçbir şey eklemek gerekmesin diye materyaller, prefablar,
/// lobi/oda/boss geometrisi, kapılar, sandık noktaları, sunaklar, Silahlık,
/// ışıklar, oyuncu, kamera, yöneticiler ve bütün referans bağlamaları
/// buradan üretilir.
///
/// Menüler her çalıştırmada "sil ve yeniden yap" mantığıyla çalışır;
/// tekrar tekrar basılabilir, bayat referans kalmaz.
/// </summary>
public static class PrototipSahneKurucu
{
    private const string Menu = "Zar Prototip/";

    private const string KokKlasor = "Assets/Prototip";
    private const string MateryalKlasor = KokKlasor + "/Materials";
    private const string PrefabKlasor = KokKlasor + "/Prefabs";
    private const string SahneKlasor = KokKlasor + "/Scenes";
    private const string SahneYolu = SahneKlasor + "/Prototip.unity";

    // Ölçüler (zemin üst yüzeyi her zaman y = 0).
    private const float OdaBoyut = 24f;
    private const float BossOdaBoyut = 30f;
    private const float LobiBoyut = 20f;
    private const float DuvarYuksekligi = 3f;
    private const float DuvarKalinligi = 1f;
    private const float KapiGenisligi = 3f;

    // Lobi merkezde; odalar birbirinden 100 birim uzakta.
    private const float OdaAraligi = 100f;

    private const string GeometriKokAdi = "SavasOdasi";

    /// <summary>Elle tasarlanmış oda düzenleri.</summary>
    private enum OdaTipi
    {
        Sutunlu,
        Koridorlu,
        Arena,
        Boss
    }

    // =====================================================================
    // 1 - Klasörler
    // =====================================================================

    [MenuItem(Menu + "1 - Klasörleri Oluştur", false, 1)]
    public static void KlasorleriOlustur()
    {
        string[] klasorler =
        {
            KokKlasor,
            KokKlasor + "/Editor",
            KokKlasor + "/Scripts",
            KokKlasor + "/Scripts/Player",
            KokKlasor + "/Scripts/Camera",
            KokKlasor + "/Scripts/Dice",
            KokKlasor + "/Scripts/Enemies",
            KokKlasor + "/Scripts/Core",
            KokKlasor + "/Scripts/UI",
            KokKlasor + "/Scripts/Items",
            KokKlasor + "/Scripts/VFX",
            SahneKlasor,
            MateryalKlasor,
            PrefabKlasor,
            KokKlasor + "/Models",
            KokKlasor + "/VFX",
            KokKlasor + "/Audio"
        };

        foreach (string klasor in klasorler) KlasorGarantile(klasor);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Zar Prototip] Klasörler hazır.");
    }

    private static void KlasorGarantile(string yol)
    {
        if (string.IsNullOrEmpty(yol) || yol == "Assets") return;
        if (AssetDatabase.IsValidFolder(yol)) return;

        string ebeveyn = Path.GetDirectoryName(yol);
        if (string.IsNullOrEmpty(ebeveyn)) return;
        ebeveyn = ebeveyn.Replace('\\', '/');

        KlasorGarantile(ebeveyn);
        AssetDatabase.CreateFolder(ebeveyn, Path.GetFileName(yol));
    }

    // =====================================================================
    // 2 - Sadece savaş odası geometrisi (hızlı göz kontrolü)
    // =====================================================================

    [MenuItem(Menu + "2 - Savaş Odasını Kur", false, 2)]
    public static void SavasOdasiniKur()
    {
        KlasorleriOlustur();
        Materyaller m = MateryalleriOlustur();

        GameObject eski = GameObject.Find(GeometriKokAdi);
        if (eski != null) Object.DestroyImmediate(eski);

        GameObject kok = new GameObject(GeometriKokAdi);
        GameObject oda = OdaGeometrisi(kok.transform, "Oda_Sutunlu", Vector3.zero, OdaTipi.Sutunlu, m);
        YonluIsik(kok.transform);
        IsiklariKur(kok.transform, Vector3.zero, 7f);
        OrtamIsigiAyarla();

        Selection.activeGameObject = oda;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[Zar Prototip] Sütunlu oda geometrisi kuruldu (oyuncu/yönetici yok). " +
                  "Oynanabilir kurulum için \"3 - Tam Prototipi Kur\" menüsünü kullan.");
    }

    // =====================================================================
    // 3 - Tam prototip
    // =====================================================================

    [MenuItem(Menu + "3 - Tam Prototipi Kur", false, 3)]
    public static void TamPrototipiKur()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        KlasorleriOlustur();

        // Prefablar ve geçici nesneler kullanıcının sahnesini kirletmesin, materyal
        // silme işi açık sahnedeki referansları bozmasın diye önce boş sahne açılıyor.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Materyaller m = MateryalleriOlustur();
        Prefablar p = PrefablariOlustur(m);
        OrtamIsigiAyarla();

        // --- Kökler ---
        GameObject ortamKok = new GameObject("Ortam");     // NavMesh pişirmesine giren tek kök
        GameObject kapiKok = new GameObject("Kapilar");    // pişirmeye girmez
        GameObject isikKok = new GameObject("Isiklar");
        GameObject etkilesimKok = new GameObject("Etkilesimler");
        GameObject odaKok = new GameObject("Odalar");
        GameObject yoneticiler = new GameObject("Yoneticiler");

        // --- Lobi ---
        Vector3 lobiMerkez = Vector3.zero;
        GameObject lobiGeometri = new GameObject("Lobi");
        lobiGeometri.transform.SetParent(ortamKok.transform, false);
        lobiGeometri.transform.localPosition = lobiMerkez;
        ZeminVeDuvarlar(lobiGeometri.transform, m, LobiBoyut, false);

        IsiklariKur(isikKok.transform, lobiMerkez, 6f);
        Transform lobiDogma = Nokta("LobiDogma", yoneticiler.transform, lobiMerkez + new Vector3(0f, 0.1f, 0f));

        // --- Normal odalar ---
        OdaTipi[] tipler = { OdaTipi.Sutunlu, OdaTipi.Koridorlu, OdaTipi.Arena };
        Object[] odalar = new Object[tipler.Length];

        for (int i = 0; i < tipler.Length; i++)
        {
            Vector3 merkez = new Vector3(OdaAraligi * (i + 1), 0f, 0f);
            odalar[i] = OdaKur(tipler[i], merkez, m, p,
                ortamKok.transform, kapiKok.transform, isikKok.transform, odaKok.transform);
        }

        // --- Boss odası (her koşunun sonu) ---
        Vector3 bossMerkez = new Vector3(OdaAraligi * (tipler.Length + 1), 0f, 0f);
        DungeonRoom bossOda = OdaKur(OdaTipi.Boss, bossMerkez, m, p,
            ortamKok.transform, kapiKok.transform, isikKok.transform, odaKok.transform);

        // --- Tek NavMeshSurface, sadece ortam geometrisini toplar ---
        NavMeshSurface yuzey = ortamKok.AddComponent<NavMeshSurface>();
        yuzey.collectObjects = CollectObjects.Children;
        yuzey.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        yuzey.defaultArea = 0;
        yuzey.layerMask = ~0;

        NavMeshBootstrap boot = ortamKok.AddComponent<NavMeshBootstrap>();
        new Baglayici(boot).Nesne("surface", yuzey).Uygula();

        // --- Yönlü ışık ve son işlem ---
        YonluIsik(isikKok.transform);
        SonIslemKur(isikKok.transform);

        // --- Oyuncu ---
        Vector3 oyuncuKonum = lobiMerkez + new Vector3(0f, 0.1f, 0f);
        GameObject oyuncu = OyuncuKur(m, oyuncuKonum,
            out PlayerController kontrol, out PlayerHealth can, out PlayerInteractor etkilesim,
            out Transform gogus, out Renderer[] oyuncuGorseller);

        // --- Kamera ---
        KameraKur(oyuncuKonum, out IsometricCamera isoKamera);

        // --- Menzil göstergesi (zar sayısı kaç olursa olsun tek çizgi) ---
        AimIndicator gosterge = MenzilGostergesiKur(m);

        // --- Yöneticiler ---
        Fx fx = yoneticiler.AddComponent<Fx>();
        PrototipHud hud = yoneticiler.AddComponent<PrototipHud>();
        PlayerProgress ilerleme = yoneticiler.AddComponent<PlayerProgress>();
        PlayerLoadout donanim = yoneticiler.AddComponent<PlayerLoadout>();
        RunState kosuDurumu = yoneticiler.AddComponent<RunState>();
        CombatModifiers carpanlar = yoneticiler.AddComponent<CombatModifiers>();
        ChestPanel sandikPaneli = yoneticiler.AddComponent<ChestPanel>();
        ArmoryPanel silahlikPaneli = yoneticiler.AddComponent<ArmoryPanel>();
        DiceManager zarHavuzu = yoneticiler.AddComponent<DiceManager>();
        DungeonManager zindan = yoneticiler.AddComponent<DungeonManager>();
        yoneticiler.AddComponent<DebugKeys>();

        // --- Lobi etkileşimleri ---
        DungeonEntrance giris = ZindanGirisiKur(etkilesimKok.transform, lobiMerkez + new Vector3(0f, 0f, 7f), m);
        Armory silahlik = SilahlikKur(etkilesimKok.transform, lobiMerkez + new Vector3(-8f, 0f, 2f), m);

        SunakKur(etkilesimKok.transform, lobiMerkez + new Vector3(-6.5f, 0f, -6.5f), StatType.Strength, m.kristalGuc, m);
        SunakKur(etkilesimKok.transform, lobiMerkez + new Vector3(-2.2f, 0f, -8f), StatType.Agility, m.kristalCeviklik, m);
        SunakKur(etkilesimKok.transform, lobiMerkez + new Vector3(2.2f, 0f, -8f), StatType.Intellect, m.kristalZeka, m);
        SunakKur(etkilesimKok.transform, lobiMerkez + new Vector3(6.5f, 0f, -6.5f), StatType.Endurance, m.kristalDayaniklilik, m);

        // --- Bütün referansları bağla ---
        new Baglayici(kontrol)
            .Nesne("aimCamera", isoKamera.Cam)
            .Nesne("isoCamera", isoKamera)
            .Nesne("chestPoint", gogus)
            .Uygula();

        new Baglayici(can)
            .Nesne("playerController", kontrol)
            .Dizi("bodyRenderers", oyuncuGorseller)
            .Uygula();

        new Baglayici(isoKamera)
            .Nesne("target", oyuncu.transform)
            .Uygula();

        new Baglayici(fx)
            .Nesne("damageTextPrefab", p.hasarYazisi)
            .Nesne("hitBurstPrefab", p.vurusPatlama)
            .Nesne("critBurstPrefab", p.kritikPatlama)
            .Nesne("deathBurstPrefab", p.olumPatlama)
            .Nesne("burnFlamePrefab", p.alev)
            .Nesne("blastBurstPrefab", p.patlamaEfekti)
            .Nesne("bossDeathPrefab", p.bossOlum)
            .Nesne("chestAppearPrefab", p.sandikBelirme)
            .Nesne("targetCamera", isoKamera)
            .Uygula();

        new Baglayici(zarHavuzu)
            .Nesne("dicePrefab", p.zar)
            .Nesne("player", kontrol)
            .Uygula();

        new Baglayici(gosterge)
            .Nesne("player", kontrol)
            .Nesne("diceManager", zarHavuzu)
            .Uygula();

        new Baglayici(hud)
            .Nesne("playerHealth", can)
            .Nesne("diceManager", zarHavuzu)
            .Nesne("dungeon", zindan)
            .Nesne("interactor", etkilesim)
            .Nesne("runState", kosuDurumu)
            .Nesne("loadout", donanim)
            .Uygula();

        new Baglayici(zindan)
            .Dizi("rooms", odalar)
            .Nesne("bossRoom", bossOda)
            .Nesne("lobbySpawn", lobiDogma)
            .Nesne("player", kontrol)
            .Nesne("playerHealth", can)
            .Nesne("diceManager", zarHavuzu)
            .Nesne("runState", kosuDurumu)
            .Uygula();

        new Baglayici(giris)
            .Nesne("dungeon", zindan)
            .Uygula();

        new Baglayici(silahlik)
            .Nesne("panel", silahlikPaneli)
            .Uygula();

        isoKamera.ApplySettings();

        // Bu bileşenlerin varsayılanları spesifikasyondakiyle aynı; ek ayar gerekmiyor.
        EditorUtility.SetDirty(ilerleme);
        EditorUtility.SetDirty(carpanlar);
        EditorUtility.SetDirty(sandikPaneli);
        EditorUtility.SetDirty(silahlikPaneli);

        // --- Sahneyi kaydet ---
        KlasorGarantile(SahneKlasor);
        UnityEngine.SceneManagement.Scene sahne = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(sahne);
        EditorSceneManager.SaveScene(sahne, SahneYolu);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = oyuncu;
        Debug.Log("[Zar Prototip] Tam prototip kuruldu ve kaydedildi: " + SahneYolu +
                  " — Lobide başlıyorsun. Silahlık'ta eşya seç, sonra zindan girişine git.");
    }

    // =====================================================================
    // 4 - Derleme ayarları
    // =====================================================================

    [MenuItem(Menu + "4 - Derleme Ayarlarını Hazırla", false, 4)]
    public static void DerlemeAyarlariniHazirla()
    {
        SceneAsset sahne = AssetDatabase.LoadAssetAtPath<SceneAsset>(SahneYolu);
        if (sahne == null)
        {
            Debug.LogError("[Zar Prototip] Sahne bulunamadı: " + SahneYolu +
                           " — Önce \"3 - Tam Prototipi Kur\" menüsünü çalıştır.");
            return;
        }

        // Yalnızca prototip sahnesi derlemeye girsin; şablon SampleScene çıksın.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SahneYolu, true) };

        PlayerSettings.productName = "Zar Prototip";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        AssetDatabase.SaveAssets();

        Debug.Log("[Zar Prototip] Derleme ayarları hazır:\n" +
                  "  • Sahne listesi: yalnızca " + SahneYolu + " (index 0)\n" +
                  "  • Pencere: 1600x900, Windowed, yeniden boyutlandırılabilir\n" +
                  "ELLE YAPILMASI GEREKENLER:\n" +
                  "  • Project Settings > Player > Color Space = Linear (bloom ve emisyon için)\n" +
                  "  • Project Settings > Quality > Render Pipeline Asset = PC_RPAsset\n" +
                  "  • PC_Renderer varlığında Post-processing açık olmalı\n" +
                  "  • Project Settings > Player > Splash Image > Show Splash Screen kapat (lisans izin veriyorsa)");
    }

    // =====================================================================
    // Oda kurulumu
    // =====================================================================

    private static DungeonRoom OdaKur(OdaTipi tip, Vector3 merkez, Materyaller m, Prefablar p,
        Transform ortamKok, Transform kapiKok, Transform isikKok, Transform odaKok)
    {
        bool boss = tip == OdaTipi.Boss;
        float boyut = boss ? BossOdaBoyut : OdaBoyut;

        OdaGeometrisi(ortamKok, "Oda_" + tip, merkez, tip, m);
        IsiklariKur(isikKok, merkez, boss ? 10f : 7f);

        RoomDoor kapi = KapiKur(kapiKok, merkez, m, tip, boyut);

        GameObject kokGo = new GameObject("Oda_" + tip);
        kokGo.transform.SetParent(odaKok, false);
        kokGo.transform.localPosition = merkez;

        float yari = boyut * 0.5f;
        Transform giris = Nokta("OyuncuGiris", kokGo.transform, new Vector3(0f, 0.1f, -(yari - 3f)));
        Transform sandik = Nokta("SandikNoktasi", kokGo.transform, SandikKonumu(tip));

        Vector3[] dogmaKonumlari = DogmaKonumlari(tip);
        Object[] dogmalar = new Object[dogmaKonumlari.Length];
        for (int i = 0; i < dogmaKonumlari.Length; i++)
        {
            dogmalar[i] = Nokta("DusmanDogma" + (i + 1), kokGo.transform, dogmaKonumlari[i]);
        }

        DungeonRoom oda = kokGo.AddComponent<DungeonRoom>();

        HavuzAyari(tip, out float kovalayanAgirlik, out int enAz, out int enCok);

        new Baglayici(oda)
            .Metin("roomName", OdaAdi(tip))
            .Dogru("bossRoom", boss)
            .Nesne("playerEntry", giris)
            .Dizi("enemySpawns", dogmalar)
            .Nesne("chestSpawn", sandik)
            .Nesne("chaserPrefab", p.kovalayan)
            .Nesne("shooterPrefab", p.mermiAtan)
            .Nesne("bossPrefab", boss ? p.boss : null)
            .Sayi("chaserWeight", kovalayanAgirlik)
            .TamSayi("minEnemies", enAz)
            .TamSayi("maxEnemies", enCok)
            .Nesne("chestPrefab", p.sandik)
            .Nesne("door", kapi)
            .Uygula();

        return oda;
    }

    private static string OdaAdi(OdaTipi tip)
    {
        switch (tip)
        {
            case OdaTipi.Sutunlu: return "Sütunlu Oda";
            case OdaTipi.Koridorlu: return "Koridorlu Oda";
            case OdaTipi.Arena: return "Arena";
            default: return "Zindan Bekçisi";
        }
    }

    /// <summary>Oda havuzu: hangi düşman ağırlıklı ve kaç tane.</summary>
    private static void HavuzAyari(OdaTipi tip, out float kovalayanAgirlik, out int enAz, out int enCok)
    {
        switch (tip)
        {
            case OdaTipi.Sutunlu:
                kovalayanAgirlik = 0.8f;    // ağırlıklı Kovalayan
                enAz = 3;
                enCok = 4;
                break;

            case OdaTipi.Koridorlu:
                kovalayanAgirlik = 0.25f;   // ağırlıklı Mermi atan
                enAz = 3;
                enCok = 4;
                break;

            case OdaTipi.Arena:
                kovalayanAgirlik = 0.5f;    // karışık, daha kalabalık
                enAz = 4;
                enCok = 6;
                break;

            default:
                kovalayanAgirlik = 0f;      // boss odasında havuz kullanılmaz
                enAz = 1;
                enCok = 1;
                break;
        }
    }

    private static Vector3 SandikKonumu(OdaTipi tip)
    {
        // Sandığın taban pedi yerde dursun diye y neredeyse sıfır.
        switch (tip)
        {
            case OdaTipi.Sutunlu: return new Vector3(0f, 0.02f, 0f);
            case OdaTipi.Koridorlu: return new Vector3(0f, 0.02f, -2f);
            case OdaTipi.Arena: return new Vector3(-6f, 0.02f, 0f);
            default: return new Vector3(0f, 0.02f, -2f);
        }
    }

    private static Vector3[] DogmaKonumlari(OdaTipi tip)
    {
        switch (tip)
        {
            case OdaTipi.Sutunlu:
                return new[]
                {
                    new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, 8f),
                    new Vector3(-8f, 0f, 1f), new Vector3(8f, 0f, 1f),
                    new Vector3(0f, 0f, 9f), new Vector3(0f, 0f, 3f)
                };

            case OdaTipi.Koridorlu:
                // Üç şeridin her birine düşman düşsün.
                return new[]
                {
                    new Vector3(-8f, 0f, 0f), new Vector3(-8f, 0f, 4f),
                    new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, 9f),
                    new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 4f)
                };

            case OdaTipi.Arena:
                return new[]
                {
                    new Vector3(-9f, 0f, -5f), new Vector3(9f, 0f, -5f),
                    new Vector3(-9f, 0f, 5f), new Vector3(9f, 0f, 5f),
                    new Vector3(-9f, 0f, 0f), new Vector3(9f, 0f, 0f),
                    new Vector3(0f, 0f, 9f)
                };

            default:
                // Boss odasında ilk nokta boss'un doğma yeri.
                return new[] { new Vector3(0f, 0f, 7f) };
        }
    }

    private static GameObject OdaGeometrisi(Transform ebeveyn, string ad, Vector3 merkez, OdaTipi tip, Materyaller m)
    {
        GameObject kok = new GameObject(ad);
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.localPosition = merkez;

        float boyut = tip == OdaTipi.Boss ? BossOdaBoyut : OdaBoyut;
        ZeminVeDuvarlar(kok.transform, m, boyut, true);

        switch (tip)
        {
            case OdaTipi.Sutunlu:
                // Dört sütun: hem siper hem zar atışını zorlaştıran engel.
                Kutu("Sutun1", kok.transform, new Vector3(-5f, 1.5f, -5f), new Vector3(2f, DuvarYuksekligi, 2f), m.sutun);
                Kutu("Sutun2", kok.transform, new Vector3(5f, 1.5f, -5f), new Vector3(2f, DuvarYuksekligi, 2f), m.sutun);
                Kutu("Sutun3", kok.transform, new Vector3(-5f, 1.5f, 5f), new Vector3(2f, DuvarYuksekligi, 2f), m.sutun);
                Kutu("Sutun4", kok.transform, new Vector3(5f, 1.5f, 5f), new Vector3(2f, DuvarYuksekligi, 2f), m.sutun);
                break;

            case OdaTipi.Koridorlu:
                // Uzun duvarlar odayı üç şeride böler; iki uçtan birbirine bağlı.
                Kutu("Koridor1", kok.transform, new Vector3(-4.5f, 1.5f, -2f), new Vector3(1f, DuvarYuksekligi, 14f), m.duvar);
                Kutu("Koridor2", kok.transform, new Vector3(4.5f, 1.5f, -2f), new Vector3(1f, DuvarYuksekligi, 14f), m.duvar);
                break;

            case OdaTipi.Arena:
                // Arena: ortada tek blok, geniş açık alan.
                Kutu("MerkezBlok", kok.transform, new Vector3(0f, 1.5f, 0f), new Vector3(4f, DuvarYuksekligi, 4f), m.sutun);
                break;

            default:
                // Boss odası: geniş açık alan, köşelerde birkaç siper.
                Kutu("Siper1", kok.transform, new Vector3(-10f, 1.5f, -10f), new Vector3(3f, DuvarYuksekligi, 3f), m.sutun);
                Kutu("Siper2", kok.transform, new Vector3(10f, 1.5f, -10f), new Vector3(3f, DuvarYuksekligi, 3f), m.sutun);
                Kutu("Siper3", kok.transform, new Vector3(-10f, 1.5f, 10f), new Vector3(3f, DuvarYuksekligi, 3f), m.sutun);
                Kutu("Siper4", kok.transform, new Vector3(10f, 1.5f, 10f), new Vector3(3f, DuvarYuksekligi, 3f), m.sutun);
                break;
        }

        return kok;
    }

    private static void ZeminVeDuvarlar(Transform kok, Materyaller m, float boyut, bool kapiBoslugu)
    {
        float yari = boyut * 0.5f;
        float duvarOfset = yari + DuvarKalinligi * 0.5f;
        float uzunluk = boyut + DuvarKalinligi * 2f;
        float duvarY = DuvarYuksekligi * 0.5f;

        // Zemin: üst yüzeyi tam y = 0 olacak şekilde aşağı kaydırılmış 1 birim kalınlık.
        Kutu("Zemin", kok, new Vector3(0f, -0.5f, 0f), new Vector3(boyut, 1f, boyut), m.zemin);

        Kutu("DuvarGuney", kok, new Vector3(0f, duvarY, -duvarOfset),
            new Vector3(uzunluk, DuvarYuksekligi, DuvarKalinligi), m.duvar);
        Kutu("DuvarBati", kok, new Vector3(-duvarOfset, duvarY, 0f),
            new Vector3(DuvarKalinligi, DuvarYuksekligi, uzunluk), m.duvar);
        Kutu("DuvarDogu", kok, new Vector3(duvarOfset, duvarY, 0f),
            new Vector3(DuvarKalinligi, DuvarYuksekligi, uzunluk), m.duvar);

        if (!kapiBoslugu)
        {
            Kutu("DuvarKuzey", kok, new Vector3(0f, duvarY, duvarOfset),
                new Vector3(uzunluk, DuvarYuksekligi, DuvarKalinligi), m.duvar);
            return;
        }

        // Kuzey duvarı ortada 3 birimlik kapı boşluğu bırakacak şekilde iki parça.
        float parca = (uzunluk - KapiGenisligi) * 0.5f;
        float parcaMerkez = (KapiGenisligi + parca) * 0.5f;

        Kutu("DuvarKuzeySol", kok, new Vector3(-parcaMerkez, duvarY, duvarOfset),
            new Vector3(parca, DuvarYuksekligi, DuvarKalinligi), m.duvar);
        Kutu("DuvarKuzeySag", kok, new Vector3(parcaMerkez, duvarY, duvarOfset),
            new Vector3(parca, DuvarYuksekligi, DuvarKalinligi), m.duvar);

        // Kapı eşiği zemini: oyuncu boşluktan geçerken aşağı düşmesin.
        Kutu("KapiEsigi", kok, new Vector3(0f, -0.5f, duvarOfset + 1f),
            new Vector3(KapiGenisligi, 1f, 4f), m.zemin);
    }

    /// <summary>
    /// Çıkış kapısı: boşluğu dolduran blok + durum rengini gösteren çerçeve +
    /// geçiş eşiği noktası. Ayrı kökte olduğu için NavMesh pişirmesine girmez.
    /// </summary>
    private static RoomDoor KapiKur(Transform ebeveyn, Vector3 merkez, Materyaller m, OdaTipi tip, float boyut)
    {
        float duvarOfset = boyut * 0.5f + DuvarKalinligi * 0.5f;

        GameObject kok = new GameObject("Kapi_" + tip);
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.localPosition = merkez;

        GameObject blok = Kutu("Blok", kok.transform, new Vector3(0f, DuvarYuksekligi * 0.5f, duvarOfset),
            new Vector3(KapiGenisligi, DuvarYuksekligi, DuvarKalinligi), m.kapiBlok);

        GameObject sol = Kutu("CerceveSol", kok.transform, new Vector3(-2.2f, 1.7f, duvarOfset),
            new Vector3(1.4f, 3.4f, 1.2f), m.kapiCerceve, false);
        GameObject sag = Kutu("CerceveSag", kok.transform, new Vector3(2.2f, 1.7f, duvarOfset),
            new Vector3(1.4f, 3.4f, 1.2f), m.kapiCerceve, false);
        GameObject kiris = Kutu("CerceveKiris", kok.transform, new Vector3(0f, 3.6f, duvarOfset),
            new Vector3(5.8f, 0.5f, 1.2f), m.kapiCerceve, false);

        Transform esik = Nokta("EsikNoktasi", kok.transform, new Vector3(0f, 0f, duvarOfset + 1f));

        RoomDoor kapi = kok.AddComponent<RoomDoor>();
        new Baglayici(kapi)
            .Nesne("blocker", blok)
            .Dizi("frameRenderers", new Object[]
            {
                sol.GetComponent<MeshRenderer>(),
                sag.GetComponent<MeshRenderer>(),
                kiris.GetComponent<MeshRenderer>()
            })
            .Nesne("passPoint", esik)
            .Uygula();

        return kapi;
    }

    // =====================================================================
    // Lobi etkileşimleri
    // =====================================================================

    private static DungeonEntrance ZindanGirisiKur(Transform ebeveyn, Vector3 konum, Materyaller m)
    {
        GameObject kok = new GameObject("ZindanGirisi");
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.localPosition = konum;

        Kutu("Taban", kok.transform, new Vector3(0f, 0.15f, 0f), new Vector3(5f, 0.3f, 2.2f), m.sunak, false);
        Kutu("SutunSol", kok.transform, new Vector3(-1.9f, 1.9f, 0f), new Vector3(0.8f, 3.8f, 0.8f), m.sunak);
        Kutu("SutunSag", kok.transform, new Vector3(1.9f, 1.9f, 0f), new Vector3(0.8f, 3.8f, 0.8f), m.sunak);
        Kutu("Kiris", kok.transform, new Vector3(0f, 3.9f, 0f), new Vector3(4.6f, 0.6f, 0.9f), m.sunak);

        // Parlayan geçit yüzeyi: lobinin en dikkat çeken şeyi olsun.
        Kutu("Gecit", kok.transform, new Vector3(0f, 1.9f, 0f), new Vector3(3f, 3.4f, 0.18f), m.zindanGirisi, false);

        NoktaIsik(kok.transform, new Vector3(0f, 2f, -1.2f), new Color(0.65f, 0.35f, 1f), 7f, 12f);

        DungeonEntrance giris = kok.AddComponent<DungeonEntrance>();
        new Baglayici(giris)
            .Sayi("radius", 3f)
            .Sayi("promptHeight", 2.4f)
            .Uygula();

        return giris;
    }

    private static Armory SilahlikKur(Transform ebeveyn, Vector3 konum, Materyaller m)
    {
        GameObject kok = new GameObject("Silahlik");
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.localPosition = konum;

        // Duvara dayalı bir silah rafı görünümü.
        Kutu("Taban", kok.transform, new Vector3(0f, 0.08f, 0f), new Vector3(3.2f, 0.16f, 1.4f), m.sunak, false);
        Kutu("Arkalik", kok.transform, new Vector3(0f, 1.5f, 0.55f), new Vector3(3.2f, 3f, 0.3f), m.sunak);
        Kutu("Raf1", kok.transform, new Vector3(0f, 1f, 0f), new Vector3(3f, 0.14f, 1f), m.sunak, false);
        Kutu("Raf2", kok.transform, new Vector3(0f, 2f, 0f), new Vector3(3f, 0.14f, 1f), m.sunak, false);
        Kutu("Parilti", kok.transform, new Vector3(0f, 1.5f, 0.35f), new Vector3(2.6f, 2.6f, 0.12f), m.silahlik, false);

        NoktaIsik(kok.transform, new Vector3(0f, 1.8f, -0.8f), new Color(0.6f, 0.8f, 1f), 5.5f, 9f);

        Armory silahlik = kok.AddComponent<Armory>();
        new Baglayici(silahlik)
            .Sayi("radius", 2.8f)
            .Sayi("promptHeight", 2.4f)
            .Uygula();

        return silahlik;
    }

    private static StatAltar SunakKur(Transform ebeveyn, Vector3 konum, StatType stat, Material kristalMat, Materyaller m)
    {
        GameObject kok = new GameObject("Sunak_" + stat);
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.localPosition = konum;

        Kutu("Kaide", kok.transform, new Vector3(0f, 0.7f, 0f), new Vector3(1.3f, 1.4f, 1.3f), m.sunak);
        Kure("Kristal", kok.transform, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.75f, kristalMat, false);

        NoktaIsik(kok.transform, new Vector3(0f, 1.9f, 0f), StatRengi(stat), 4.5f, 8f);

        StatAltar sunak = kok.AddComponent<StatAltar>();
        new Baglayici(sunak)
            .Numaralandirma("stat", (int)stat)
            .Sayi("radius", 2.3f)
            .Sayi("promptHeight", 2.2f)
            .Uygula();

        return sunak;
    }

    private static Color StatRengi(StatType stat)
    {
        switch (stat)
        {
            case StatType.Strength: return new Color(1f, 0.32f, 0.26f);
            case StatType.Agility: return new Color(0.4f, 1f, 0.5f);
            case StatType.Intellect: return new Color(0.42f, 0.6f, 1f);
            default: return new Color(1f, 0.85f, 0.35f);
        }
    }

    // =====================================================================
    // Işıklar ve son işlem
    // =====================================================================

    private static void YonluIsik(Transform kok)
    {
        // Loş, soğuk yönlü ışık: karanlık fantastik hava.
        GameObject go = new GameObject("YonluIsik");
        go.transform.SetParent(kok, false);
        go.transform.localPosition = new Vector3(0f, 14f, 0f);
        go.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

        Light isik = go.AddComponent<Light>();
        isik.type = LightType.Directional;
        isik.color = new Color(0.55f, 0.62f, 0.88f);
        isik.intensity = 0.38f;
        isik.shadows = LightShadows.Soft;
    }

    /// <summary>Bir alanın sıcak renkli nokta ışıkları ve soğuk dolgu ışığı.</summary>
    private static void IsiklariKur(Transform kok, Vector3 merkez, float ofset)
    {
        GameObject grup = new GameObject("Isiklar_" + Mathf.RoundToInt(merkez.x));
        grup.transform.SetParent(kok, false);
        grup.transform.localPosition = merkez;

        Vector3[] noktalar =
        {
            new Vector3(-ofset, 2.6f, -ofset),
            new Vector3(ofset, 2.6f, ofset),
            new Vector3(ofset, 2.6f, -ofset),
            new Vector3(-ofset, 2.6f, ofset)
        };

        for (int i = 0; i < noktalar.Length; i++)
        {
            NoktaIsik(grup.transform, noktalar[i], new Color(1f, 0.48f, 0.18f), 7.5f, ofset * 2.2f);
        }

        // Merkeze soğuk, çok loş bir dolgu ışığı: köşeler tamamen kararmasın.
        NoktaIsik(grup.transform, new Vector3(0f, 6f, 0f), new Color(0.4f, 0.5f, 0.85f), 2.2f, ofset * 3.6f);
    }

    private static Light NoktaIsik(Transform ebeveyn, Vector3 konum, Color renk, float siddet, float menzil)
    {
        GameObject go = new GameObject("NoktaIsik");
        go.transform.SetParent(ebeveyn, false);
        go.transform.localPosition = konum;

        Light isik = go.AddComponent<Light>();
        isik.type = LightType.Point;
        isik.color = renk;
        isik.intensity = siddet;
        isik.range = menzil;
        isik.shadows = LightShadows.None;

        return isik;
    }

    /// <summary>
    /// URP son işlem kurulumu: bloom, vignette, tonemapping ve renk ayarı.
    /// Emisyonlu malzemeler (zar, sonuç çerçevesi, sandık, geçit, efektler)
    /// 1'in üzerinde parladığı için bloom eşiğini aşar.
    ///
    /// Bu metot hata verirse gövdesini silmek yeterli: oyun post-processing
    /// olmadan da aynı oynanır, sadece daha sönük görünür.
    /// </summary>
    private static void SonIslemKur(Transform kok)
    {
        string yol = KokKlasor + "/VFX/PrototipVolume.asset";
        AssetDatabase.DeleteAsset(yol);

        UnityEngine.Rendering.VolumeProfile profil =
            ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(profil, yol);

        Tonemapping tone = VolumeBileseni<Tonemapping>(profil);
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.Neutral;

        Bloom bloom = VolumeBileseni<Bloom>(profil);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.15f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.85f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.72f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1f, 0.93f, 0.82f);

        Vignette vignette = VolumeBileseni<Vignette>(profil);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.38f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;

        ColorAdjustments renk = VolumeBileseni<ColorAdjustments>(profil);
        renk.contrast.overrideState = true;
        renk.contrast.value = 14f;
        renk.saturation.overrideState = true;
        renk.saturation.value = -6f;

        EditorUtility.SetDirty(profil);
        AssetDatabase.SaveAssets();

        GameObject go = new GameObject("SonIslemVolume");
        go.transform.SetParent(kok, false);

        UnityEngine.Rendering.Volume volume = go.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profil;
    }

    /// <summary>
    /// Volume bileşenini profile ekler VE varlık dosyasına yazar.
    /// VolumeProfile.Add tek başına alt varlığı kaydetmez; AddObjectToAsset olmadan
    /// ayarlar Unity yeniden yüklenince kaybolur.
    /// </summary>
    private static T VolumeBileseni<T>(UnityEngine.Rendering.VolumeProfile profil)
        where T : UnityEngine.Rendering.VolumeComponent
    {
        T bilesen = profil.Add<T>(true);
        bilesen.name = typeof(T).Name;
        bilesen.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(bilesen, profil);
        return bilesen;
    }

    private static void OrtamIsigiAyarla()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.052f, 0.054f, 0.082f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
    }

    // =====================================================================
    // Oyuncu ve kamera
    // =====================================================================

    private static GameObject OyuncuKur(Materyaller m, Vector3 konum,
        out PlayerController kontrol, out PlayerHealth can, out PlayerInteractor etkilesim,
        out Transform gogus, out Renderer[] gorseller)
    {
        GameObject kok = new GameObject("Oyuncu");
        kok.transform.position = konum;

        // Pivot ayak hizasında: height 2, radius 0.5, center (0, 1, 0).
        CharacterController cc = kok.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 1f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.04f;
        cc.minMoveDistance = 0f;

        GameObject govde = Kapsul("Govde", kok.transform, new Vector3(0f, 1f, 0f), Vector3.one, m.oyuncu, false);
        GameObject yon = Kutu("Yon", kok.transform, new Vector3(0f, 1.15f, 0.55f),
            new Vector3(0.34f, 0.26f, 0.5f), m.oyuncuYon, false);

        gogus = Nokta("GogusNoktasi", kok.transform, new Vector3(0f, 1.2f, 0f));

        kontrol = kok.AddComponent<PlayerController>();
        can = kok.AddComponent<PlayerHealth>();
        etkilesim = kok.AddComponent<PlayerInteractor>();

        gorseller = new Renderer[]
        {
            govde.GetComponent<MeshRenderer>(),
            yon.GetComponent<MeshRenderer>()
        };

        return kok;
    }

    private static GameObject KameraKur(Vector3 hedefKonum, out IsometricCamera iso)
    {
        GameObject go = new GameObject("IzometrikKamera");
        go.tag = "MainCamera";

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 9f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.012f, 0.012f, 0.022f);
        cam.allowHDR = true;

        UniversalAdditionalCameraData urpVeri = go.AddComponent<UniversalAdditionalCameraData>();
        urpVeri.renderPostProcessing = true;
        urpVeri.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        urpVeri.renderShadows = true;

        iso = go.AddComponent<IsometricCamera>();

        Quaternion rot = Quaternion.Euler(50f, 45f, 0f);
        go.transform.SetPositionAndRotation(hedefKonum - rot * Vector3.forward * 30f, rot);

        return go;
    }

    private static AimIndicator MenzilGostergesiKur(Materyaller m)
    {
        GameObject kok = new GameObject("MenzilGostergesi");

        LineRenderer cizgi = kok.AddComponent<LineRenderer>();
        CizgiAyarla(cizgi, m.menzilCizgisi, 0.09f,
            new Color(1f, 0.85f, 0.45f), 0.75f, new Color(1f, 0.45f, 0.12f), 0f);
        cizgi.enabled = false;

        return kok.AddComponent<AimIndicator>();
    }

    private static void CizgiAyarla(LineRenderer cizgi, Material mat, float genislik,
        Color basRenk, float basAlfa, Color sonRenk, float sonAlfa)
    {
        cizgi.sharedMaterial = mat;
        cizgi.useWorldSpace = true;
        cizgi.positionCount = 2;
        cizgi.widthMultiplier = genislik;
        cizgi.numCapVertices = 2;
        cizgi.alignment = LineAlignment.View;
        cizgi.textureMode = LineTextureMode.Stretch;
        cizgi.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cizgi.receiveShadows = false;
        cizgi.colorGradient = Gradient2(basRenk, basAlfa, sonRenk, sonAlfa);
    }

    // =====================================================================
    // Prefablar
    // =====================================================================

    private class Prefablar
    {
        public GameObject zar;
        public GameObject sonucGostergesi;
        public GameObject kovalayan;
        public GameObject mermiAtan;
        public GameObject boss;
        public GameObject mermi;
        public GameObject hasarYazisi;
        public GameObject vurusPatlama;
        public GameObject kritikPatlama;
        public GameObject olumPatlama;
        public GameObject patlamaEfekti;
        public GameObject bossOlum;
        public GameObject sandikBelirme;
        public GameObject alev;
        public GameObject sandik;
    }

    private static Prefablar PrefablariOlustur(Materyaller m)
    {
        Prefablar p = new Prefablar();

        p.vurusPatlama = ParcacikPrefabi("VurusPatlama", m.vurusParcacik,
            new Color(1f, 0.95f, 0.75f), 36, 8.5f, 0.28f, 0.55f);
        p.kritikPatlama = ParcacikPrefabi("KritikPatlama", m.kritikParcacik,
            new Color(1f, 0.55f, 0.12f), 120, 17f, 0.46f, 0.9f);
        p.olumPatlama = ParcacikPrefabi("OlumPatlama", m.vurusParcacik,
            new Color(0.9f, 0.3f, 0.3f), 32, 6f, 0.32f, 0.7f);
        p.patlamaEfekti = ParcacikPrefabi("PatlamaEfekti", m.kritikParcacik,
            new Color(1f, 0.42f, 0.1f), 150, 19f, 0.55f, 0.85f);
        p.bossOlum = ParcacikPrefabi("BossOlum", m.kritikParcacik,
            new Color(1f, 0.4f, 0.15f), 320, 26f, 0.85f, 1.5f);
        p.sandikBelirme = ParcacikPrefabi("SandikBelirme", m.vurusParcacik,
            new Color(1f, 0.85f, 0.4f), 60, 5f, 0.3f, 0.8f);

        p.alev = AlevPrefabi(m.kritikParcacik);

        p.hasarYazisi = HasarYazisiPrefabi();
        p.sonucGostergesi = SonucGostergesiPrefabi(m);
        p.mermi = MermiPrefabi(m);
        p.zar = ZarPrefabi(m, p.sonucGostergesi);
        p.kovalayan = KovalayanPrefabi(m);
        p.mermiAtan = MermiAtanPrefabi(m, p.mermi);
        p.boss = BossPrefabi(m, p.mermi, p.kovalayan);
        p.sandik = SandikPrefabi(m);

        AssetDatabase.SaveAssets();
        return p;
    }

    private static GameObject ZarPrefabi(Materyaller m, GameObject sonucGostergesi)
    {
        GameObject kok = new GameObject("Zar");
        Kutu("Govde", kok.transform, Vector3.zero, Vector3.one * 0.45f, m.zar, false);

        TrailRenderer iz = kok.AddComponent<TrailRenderer>();
        iz.sharedMaterial = m.zarIz;
        iz.time = 0.3f;
        iz.widthMultiplier = 0.42f;
        iz.minVertexDistance = 0.04f;
        iz.numCapVertices = 4;
        iz.alignment = LineAlignment.View;
        iz.textureMode = LineTextureMode.Stretch;
        iz.emitting = false;
        iz.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        iz.receiveShadows = false;
        iz.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        iz.colorGradient = Gradient2(
            new Color(1f, 0.92f, 0.5f), 1f,
            new Color(1f, 0.4f, 0.08f), 0f);

        Dice zar = kok.AddComponent<Dice>();
        new Baglayici(zar)
            .Nesne("trail", iz)
            .Nesne("resultDisplayPrefab", sonucGostergesi)
            .Uygula();

        return PrefabKaydet(kok, "Zar");
    }

    /// <summary>
    /// Sonuç göstergesi: kare çerçeve (zar yüzü) ve üstündeki sayı.
    /// Dönerken slot makinesi gibi değişir, sonuç belirince büyür ve düşmana iner.
    /// </summary>
    private static GameObject SonucGostergesiPrefabi(Materyaller m)
    {
        GameObject kok = new GameObject("SonucGostergesi");

        GameObject gorsel = new GameObject("Gorsel");
        gorsel.transform.SetParent(kok.transform, false);

        // Yazı kameraya dönük tarafta, çerçeve plakası arkasında.
        GameObject yaziGo = new GameObject("Yazi");
        yaziGo.transform.SetParent(gorsel.transform, false);

        TextMesh yazi = yaziGo.AddComponent<TextMesh>();
        Font font = YaziFontu();
        yazi.font = font;
        yazi.text = "6";
        yazi.fontSize = 64;
        yazi.fontStyle = FontStyle.Bold;
        yazi.characterSize = 0.2f;
        yazi.anchor = TextAnchor.MiddleCenter;
        yazi.alignment = TextAlignment.Center;
        yazi.color = Color.white;

        MeshRenderer yaziRenderer = yaziGo.GetComponent<MeshRenderer>();
        if (yaziRenderer == null) yaziRenderer = yaziGo.AddComponent<MeshRenderer>();
        if (font != null) yaziRenderer.sharedMaterial = font.material;
        yaziRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        yaziRenderer.receiveShadows = false;

        GameObject cerceve = Kutu("Cerceve", gorsel.transform, new Vector3(0f, 0f, 0.08f),
            new Vector3(0.85f, 0.85f, 0.06f), m.sonucCerceve, false);

        DiceResultDisplay gostergeBilesen = kok.AddComponent<DiceResultDisplay>();
        new Baglayici(gostergeBilesen)
            .Nesne("text", yazi)
            .Nesne("frameRenderer", cerceve.GetComponent<MeshRenderer>())
            .Nesne("visual", gorsel.transform)
            .Uygula();

        return PrefabKaydet(kok, "SonucGostergesi");
    }

    private static GameObject KovalayanPrefabi(Materyaller m)
    {
        GameObject kok = new GameObject("Kovalayan");

        GameObject govde = Kapsul("Govde", kok.transform, new Vector3(0f, 1f, 0f), Vector3.one, m.kovalayan, true);
        Kutu("Goz", kok.transform, new Vector3(0f, 1.6f, 0.45f), new Vector3(0.52f, 0.14f, 0.1f), m.zar, false);

        NavMeshAgent ajan = kok.AddComponent<NavMeshAgent>();
        AjanAyarla(ajan, 0.5f, 2f, 3.4f, 1.2f);

        EnemyHealth can = kok.AddComponent<EnemyHealth>();
        new Baglayici(can)
            .TamSayi("maxHealth", 80)
            .Sayi("headHeight", 2.3f)
            .Dizi("bodyRenderers", new Object[] { govde.GetComponent<MeshRenderer>() })
            .Renk("baseColor", new Color(0.62f, 0.12f, 0.12f))
            .Renk("telegraphColor", new Color(1f, 0.78f, 0.12f))
            .Uygula();

        kok.AddComponent<ChaserEnemy>();
        CanCubuguEkle(kok.transform, 2.75f, 1.3f, m);

        return PrefabKaydet(kok, "Kovalayan");
    }

    private static GameObject MermiAtanPrefabi(Materyaller m, GameObject mermiPrefabi)
    {
        GameObject kok = new GameObject("MermiAtan");

        GameObject govde = Kapsul("Govde", kok.transform, new Vector3(0f, 1f, 0f), Vector3.one, m.mermiAtan, true);
        Kutu("Goz", kok.transform, new Vector3(0f, 1.6f, 0.45f), new Vector3(0.52f, 0.14f, 0.1f), m.mermi, false);

        NavMeshAgent ajan = kok.AddComponent<NavMeshAgent>();
        AjanAyarla(ajan, 0.5f, 2f, 3f, 0.1f);

        EnemyHealth can = kok.AddComponent<EnemyHealth>();
        new Baglayici(can)
            .TamSayi("maxHealth", 50)
            .Sayi("headHeight", 2.3f)
            .Dizi("bodyRenderers", new Object[] { govde.GetComponent<MeshRenderer>() })
            .Renk("baseColor", new Color(0.36f, 0.16f, 0.52f))
            .Renk("telegraphColor", new Color(1f, 0.3f, 0.75f))
            .Uygula();

        ShooterEnemy atici = kok.AddComponent<ShooterEnemy>();
        new Baglayici(atici)
            .Nesne("projectilePrefab", mermiPrefabi)
            .Uygula();

        CanCubuguEkle(kok.transform, 2.75f, 1.3f, m);

        return PrefabKaydet(kok, "MermiAtan");
    }

    /// <summary>Zindan Bekçisi: diğer düşmanların yaklaşık 2,5 katı, farklı renk.</summary>
    private static GameObject BossPrefabi(Materyaller m, GameObject mermiPrefabi, GameObject kovalayanPrefabi)
    {
        GameObject kok = new GameObject("Boss");

        const float olcek = 2.5f;

        GameObject govde = Kapsul("Govde", kok.transform, new Vector3(0f, olcek, 0f),
            Vector3.one * olcek, m.boss, true);
        Kutu("Goz", kok.transform, new Vector3(0f, olcek * 1.55f, olcek * 0.45f),
            new Vector3(1.3f, 0.28f, 0.25f), m.kritikParcacikKati, false);

        NavMeshAgent ajan = kok.AddComponent<NavMeshAgent>();
        AjanAyarla(ajan, 0.9f, olcek * 2f, 2.6f, 1.5f);

        EnemyHealth can = kok.AddComponent<EnemyHealth>();
        new Baglayici(can)
            .TamSayi("maxHealth", 600)
            .Sayi("headHeight", olcek * 2.1f)
            .Dizi("bodyRenderers", new Object[] { govde.GetComponent<MeshRenderer>() })
            .Renk("baseColor", new Color(0.3f, 0.12f, 0.42f))
            .Renk("telegraphColor", new Color(1f, 0.85f, 0.2f))
            .Renk("enragedColor", new Color(1f, 0.18f, 0.1f))
            .Renk("stunnedColor", new Color(0.45f, 0.55f, 0.85f))
            .Dogru("isBoss", true)
            .Sayi("flameHeight", olcek)
            .Uygula();

        // Hücum yönünü gösteren yer çizgisi.
        GameObject cizgiGo = new GameObject("HucumCizgisi");
        cizgiGo.transform.SetParent(kok.transform, false);
        LineRenderer cizgi = cizgiGo.AddComponent<LineRenderer>();
        CizgiAyarla(cizgi, m.hucumCizgisi, 1.1f,
            new Color(1f, 0.25f, 0.15f), 0.85f, new Color(1f, 0.6f, 0.1f), 0.1f);
        cizgi.enabled = false;

        BossEnemy boss = kok.AddComponent<BossEnemy>();
        new Baglayici(boss)
            .Metin("bossName", "Zindan Bekçisi")
            .Nesne("chargeLine", cizgi)
            .Nesne("projectilePrefab", mermiPrefabi)
            .Nesne("summonPrefab", kovalayanPrefabi)
            .Sayi("muzzleHeight", olcek * 0.9f)
            .Uygula();

        CanCubuguEkle(kok.transform, olcek * 2.4f, 3.2f, m);

        return PrefabKaydet(kok, "Boss");
    }

    /// <summary>
    /// Düşmanın üstüne can çubuğu ekler. Dolgu, sol kenarda duran pivotun
    /// x ölçeğiyle büyütülüp küçültülür; birim genişlik 1'dir.
    /// </summary>
    private static EnemyHealthBar CanCubuguEkle(Transform ebeveyn, float yukseklik, float genislik, Materyaller m)
    {
        GameObject kok = new GameObject("CanCubugu");
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.localPosition = new Vector3(0f, yukseklik, 0f);

        GameObject gorsel = new GameObject("Gorsel");
        gorsel.transform.SetParent(kok.transform, false);
        gorsel.transform.localScale = new Vector3(genislik, 1f, 1f);

        Kutu("Arka", gorsel.transform, new Vector3(0f, 0f, 0.02f),
            new Vector3(1.06f, 0.17f, 0.03f), m.canArka, false);

        GameObject kayipPivot = new GameObject("KayipPivot");
        kayipPivot.transform.SetParent(gorsel.transform, false);
        kayipPivot.transform.localPosition = new Vector3(-0.5f, 0f, 0.01f);
        Kutu("Kayip", kayipPivot.transform, new Vector3(0.5f, 0f, 0f),
            new Vector3(1f, 0.12f, 0.03f), m.canKayip, false);

        GameObject dolguPivot = new GameObject("DolguPivot");
        dolguPivot.transform.SetParent(gorsel.transform, false);
        dolguPivot.transform.localPosition = new Vector3(-0.5f, 0f, 0f);
        Kutu("Dolgu", dolguPivot.transform, new Vector3(0.5f, 0f, 0f),
            new Vector3(1f, 0.12f, 0.03f), m.canDolgu, false);

        EnemyHealthBar cubuk = kok.AddComponent<EnemyHealthBar>();
        new Baglayici(cubuk)
            .Nesne("fillPivot", dolguPivot.transform)
            .Nesne("ghostPivot", kayipPivot.transform)
            .Nesne("visual", gorsel.transform)
            .Uygula();

        return cubuk;
    }

    private static void AjanAyarla(NavMeshAgent ajan, float yaricap, float yukseklik, float hiz, float durmaMesafesi)
    {
        ajan.radius = yaricap;
        ajan.height = yukseklik;
        ajan.baseOffset = 0f;
        ajan.speed = hiz;
        ajan.acceleration = 20f;
        ajan.angularSpeed = 720f;
        ajan.stoppingDistance = durmaMesafesi;
        ajan.autoBraking = true;
        ajan.autoRepath = true;
        ajan.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    }

    private static GameObject MermiPrefabi(Materyaller m)
    {
        GameObject kok = new GameObject("Mermi");

        // Collider yok: mermi kendi küresel taramasını yapıyor, zarın hedefi olmamalı.
        Kure("Govde", kok.transform, Vector3.zero, Vector3.one * 0.38f, m.mermi, false);

        TrailRenderer iz = kok.AddComponent<TrailRenderer>();
        iz.sharedMaterial = m.mermiIz;
        iz.time = 0.18f;
        iz.widthMultiplier = 0.26f;
        iz.minVertexDistance = 0.04f;
        iz.numCapVertices = 3;
        iz.alignment = LineAlignment.View;
        iz.textureMode = LineTextureMode.Stretch;
        iz.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        iz.receiveShadows = false;
        iz.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        iz.colorGradient = Gradient2(
            new Color(1f, 0.35f, 0.9f), 0.9f,
            new Color(0.5f, 0.1f, 0.6f), 0f);

        kok.AddComponent<EnemyProjectile>();

        return PrefabKaydet(kok, "Mermi");
    }

    private static GameObject SandikPrefabi(Materyaller m)
    {
        GameObject kok = new GameObject("Sandik");

        Kutu("Taban", kok.transform, new Vector3(0f, 0.04f, 0f), new Vector3(1.6f, 0.08f, 1.6f), m.sunak, false);

        GameObject govde = new GameObject("Govde");
        govde.transform.SetParent(kok.transform, false);
        govde.transform.localPosition = new Vector3(0f, 0.42f, 0f);

        Kutu("Kasa", govde.transform, Vector3.zero, new Vector3(1.05f, 0.7f, 0.8f), m.sunak, false);
        Kutu("Kapak", govde.transform, new Vector3(0f, 0.42f, 0f), new Vector3(1.12f, 0.16f, 0.88f), m.sandik, false);

        NoktaIsik(kok.transform, new Vector3(0f, 1f, 0f), new Color(1f, 0.82f, 0.35f), 5.5f, 9f);

        TreasureChest sandik = kok.AddComponent<TreasureChest>();
        new Baglayici(sandik)
            .Nesne("body", govde.transform)
            .Sayi("radius", 2.5f)
            .Sayi("promptHeight", 1.5f)
            .Uygula();

        return PrefabKaydet(kok, "Sandik");
    }

    private static Font YaziFontu()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private static GameObject HasarYazisiPrefabi()
    {
        GameObject kok = new GameObject("HasarYazisi");

        TextMesh yazi = kok.AddComponent<TextMesh>();
        Font font = YaziFontu();

        yazi.font = font;
        yazi.text = "0";
        yazi.fontSize = 64;
        yazi.fontStyle = FontStyle.Bold;
        yazi.characterSize = 0.16f;
        yazi.anchor = TextAnchor.MiddleCenter;
        yazi.alignment = TextAlignment.Center;
        yazi.color = Color.white;

        MeshRenderer mr = kok.GetComponent<MeshRenderer>();
        if (mr == null) mr = kok.AddComponent<MeshRenderer>();
        if (font != null) mr.sharedMaterial = font.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        kok.AddComponent<DamageText>();

        return PrefabKaydet(kok, "HasarYazisi");
    }

    private static GameObject AlevPrefabi(Material mat)
    {
        GameObject kok = new GameObject("AlevEfekti");

        ParticleSystem ps = kok.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule ana = ps.main;
        ana.loop = true;
        ana.duration = 1f;
        ana.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        ana.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        ana.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        ana.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.52f, 0.12f));
        ana.gravityModifier = new ParticleSystem.MinMaxCurve(-0.4f);   // alev yukarı çıksın
        ana.simulationSpace = ParticleSystemSimulationSpace.Local;
        ana.maxParticles = 150;

        ParticleSystem.EmissionModule cikis = ps.emission;
        cikis.enabled = true;
        cikis.rateOverTime = 45f;

        ParticleSystem.ShapeModule sekil = ps.shape;
        sekil.enabled = true;
        sekil.shapeType = ParticleSystemShapeType.Sphere;
        sekil.radius = 0.38f;

        ParticleSystem.ColorOverLifetimeModule solma = ps.colorOverLifetime;
        solma.enabled = true;
        solma.color = new ParticleSystem.MinMaxGradient(Gradient2(
            new Color(1f, 0.85f, 0.4f), 1f,
            new Color(0.9f, 0.15f, 0.05f), 0f));

        ParticleSystem.SizeOverLifetimeModule kucul = ps.sizeOverLifetime;
        kucul.enabled = true;
        kucul.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

        ParcacikRenderer(kok, mat);

        return PrefabKaydet(kok, "AlevEfekti");
    }

    private static GameObject ParcacikPrefabi(string ad, Material mat, Color renk,
        int adet, float hiz, float boyut, float yasam)
    {
        GameObject kok = new GameObject(ad);

        ParticleSystem ps = kok.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule ana = ps.main;
        ana.loop = false;
        ana.duration = 0.6f;
        ana.startLifetime = new ParticleSystem.MinMaxCurve(yasam * 0.55f, yasam);
        ana.startSpeed = new ParticleSystem.MinMaxCurve(hiz * 0.35f, hiz);
        ana.startSize = new ParticleSystem.MinMaxCurve(boyut * 0.5f, boyut);
        ana.startColor = new ParticleSystem.MinMaxGradient(renk);
        ana.gravityModifier = new ParticleSystem.MinMaxCurve(0.45f);
        ana.simulationSpace = ParticleSystemSimulationSpace.World;
        ana.maxParticles = Mathf.Max(400, adet * 2);

        ParticleSystem.EmissionModule cikis = ps.emission;
        cikis.enabled = true;
        cikis.rateOverTime = 0f;
        cikis.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(adet, 1, 1000)) });

        ParticleSystem.ShapeModule sekil = ps.shape;
        sekil.enabled = true;
        sekil.shapeType = ParticleSystemShapeType.Sphere;
        sekil.radius = 0.15f;

        ParticleSystem.ColorOverLifetimeModule solma = ps.colorOverLifetime;
        solma.enabled = true;
        solma.color = new ParticleSystem.MinMaxGradient(Gradient2(Color.white, 1f, Color.white, 0f));

        ParticleSystem.SizeOverLifetimeModule kucul = ps.sizeOverLifetime;
        kucul.enabled = true;
        kucul.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        ParcacikRenderer(kok, mat);

        OneShotParticle tek = kok.AddComponent<OneShotParticle>();
        new Baglayici(tek)
            .Sayi("lifetime", yasam + 0.6f)
            .Dizi("systems", new Object[] { ps })
            .Uygula();

        return PrefabKaydet(kok, ad);
    }

    private static void ParcacikRenderer(GameObject go, Material mat)
    {
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.alignment = ParticleSystemRenderSpace.View;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
    }

    private static GameObject PrefabKaydet(GameObject gecici, string ad)
    {
        string yol = PrefabKlasor + "/" + ad + ".prefab";
        AssetDatabase.DeleteAsset(yol);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gecici, yol);
        Object.DestroyImmediate(gecici);

        if (prefab == null) Debug.LogError("[Zar Prototip] Prefab kaydedilemedi: " + yol);
        return prefab;
    }

    // =====================================================================
    // Materyaller
    // =====================================================================

    private class Materyaller
    {
        public Material zemin;
        public Material duvar;
        public Material sutun;
        public Material kapiBlok;
        public Material kapiCerceve;
        public Material sunak;
        public Material zindanGirisi;
        public Material silahlik;
        public Material sandik;
        public Material kristalGuc;
        public Material kristalCeviklik;
        public Material kristalZeka;
        public Material kristalDayaniklilik;
        public Material oyuncu;
        public Material oyuncuYon;
        public Material zar;
        public Material zarIz;
        public Material sonucCerceve;
        public Material menzilCizgisi;
        public Material hucumCizgisi;
        public Material kovalayan;
        public Material mermiAtan;
        public Material boss;
        public Material mermi;
        public Material mermiIz;
        public Material canArka;
        public Material canDolgu;
        public Material canKayip;
        public Material vurusParcacik;
        public Material kritikParcacik;
        public Material kritikParcacikKati;
    }

    private static Materyaller MateryalleriOlustur()
    {
        Materyaller m = new Materyaller();

        m.zemin = Lit("M_Zemin", new Color(0.085f, 0.085f, 0.105f), 0.22f, Color.black);
        m.duvar = Lit("M_Duvar", new Color(0.048f, 0.048f, 0.064f), 0.1f, Color.black);
        m.sutun = Lit("M_Sutun", new Color(0.125f, 0.112f, 0.13f), 0.28f, Color.black);

        m.kapiBlok = Lit("M_KapiBlok", new Color(0.05f, 0.045f, 0.06f), 0.35f, new Color(0.06f, 0.015f, 0.02f));
        m.kapiCerceve = Lit("M_KapiCerceve", new Color(0.32f, 0.05f, 0.06f), 0.4f, new Color(0.22f, 0.02f, 0.03f));

        m.sunak = Lit("M_Sunak", new Color(0.16f, 0.15f, 0.17f), 0.25f, Color.black);
        m.zindanGirisi = Lit("M_ZindanGirisi", new Color(0.4f, 0.2f, 0.8f), 0.6f, new Color(1.2f, 0.45f, 2.6f));
        m.silahlik = Lit("M_Silahlik", new Color(0.35f, 0.5f, 0.75f), 0.6f, new Color(0.5f, 0.9f, 1.8f));

        // Sandık kapağı: odanın dikkat çeken altın parıltısı.
        m.sandik = Lit("M_Sandik", new Color(1f, 0.82f, 0.4f), 0.7f, new Color(2.2f, 1.5f, 0.5f));

        m.kristalGuc = Lit("M_KristalGuc", new Color(1f, 0.32f, 0.26f), 0.7f, new Color(1.4f, 0.35f, 0.25f));
        m.kristalCeviklik = Lit("M_KristalCeviklik", new Color(0.4f, 1f, 0.5f), 0.7f, new Color(0.35f, 1.4f, 0.5f));
        m.kristalZeka = Lit("M_KristalZeka", new Color(0.42f, 0.6f, 1f), 0.7f, new Color(0.4f, 0.6f, 1.5f));
        m.kristalDayaniklilik = Lit("M_KristalDayaniklilik", new Color(1f, 0.85f, 0.35f), 0.7f, new Color(1.5f, 1.15f, 0.4f));

        m.oyuncu = Lit("M_Oyuncu", new Color(0.42f, 0.52f, 0.68f), 0.35f, Color.black);
        m.oyuncuYon = Lit("M_OyuncuYon", new Color(0.85f, 0.92f, 1f), 0.5f, new Color(0.25f, 0.3f, 0.4f));

        // Zar ve sonuç çerçevesi sahnenin en parlak şeyleri olsun.
        m.zar = Lit("M_Zar", new Color(1f, 0.94f, 0.65f), 0.75f, new Color(3.2f, 2.3f, 0.8f));
        m.sonucCerceve = Lit("M_SonucCerceve", Color.white, 0.8f, new Color(2.4f, 2.4f, 2.4f));

        m.kovalayan = Lit("M_Kovalayan", new Color(0.62f, 0.12f, 0.12f), 0.25f, new Color(0.1f, 0.02f, 0.02f));
        m.mermiAtan = Lit("M_MermiAtan", new Color(0.36f, 0.16f, 0.52f), 0.25f, new Color(0.06f, 0.02f, 0.1f));
        m.boss = Lit("M_Boss", new Color(0.3f, 0.12f, 0.42f), 0.4f, new Color(0.3f, 0.08f, 0.45f));
        m.mermi = Lit("M_Mermi", new Color(1f, 0.4f, 0.9f), 0.6f, new Color(2.4f, 0.5f, 2f));

        m.canArka = Lit("M_CanArka", new Color(0.04f, 0.04f, 0.05f), 0.1f, Color.black);
        m.canDolgu = Lit("M_CanDolgu", new Color(0.9f, 0.2f, 0.22f), 0.5f, new Color(1.1f, 0.2f, 0.22f));
        m.canKayip = Lit("M_CanKayip", new Color(1f, 0.78f, 0.78f), 0.5f, new Color(1.3f, 0.9f, 0.9f));

        m.kritikParcacikKati = Lit("M_KritikKati", new Color(1f, 0.6f, 0.2f), 0.7f, new Color(2.4f, 1.2f, 0.4f));

        m.zarIz = Parcacik("M_ZarIz", new Color(1f, 0.8f, 0.35f));
        m.mermiIz = Parcacik("M_MermiIz", new Color(1f, 0.35f, 0.9f));
        m.menzilCizgisi = Parcacik("M_MenzilCizgisi", new Color(1f, 0.7f, 0.3f));
        m.hucumCizgisi = Parcacik("M_HucumCizgisi", new Color(1f, 0.3f, 0.15f));
        m.vurusParcacik = Parcacik("M_VurusParcacik", Color.white);
        m.kritikParcacik = Parcacik("M_KritikParcacik", new Color(1f, 0.6f, 0.2f));

        AssetDatabase.SaveAssets();
        return m;
    }

    private static Material Lit(string ad, Color renk, float parlaklik, Color emisyon)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");

        Material m = new Material(s) { name = ad };

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
        if (m.HasProperty("_Color")) m.SetColor("_Color", renk);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", parlaklik);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", parlaklik);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

        if (emisyon.maxColorComponent > 0.001f)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emisyon);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        return Kaydet(m, ad);
    }

    private static Material Parcacik(string ad, Color renk)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");

        Material m = new Material(s) { name = ad };

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
        if (m.HasProperty("_Color")) m.SetColor("_Color", renk);

        // Toplamalı (additive) saydam kurulum: efektler karanlıkta parlasın.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);

        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return Kaydet(m, ad);
    }

    private static Material Kaydet(Material m, string ad)
    {
        string yol = MateryalKlasor + "/" + ad + ".mat";
        AssetDatabase.DeleteAsset(yol);
        AssetDatabase.CreateAsset(m, yol);
        return AssetDatabase.LoadAssetAtPath<Material>(yol);
    }

    // =====================================================================
    // Küçük yardımcılar
    // =====================================================================

    private static GameObject Kutu(string ad, Transform ebeveyn, Vector3 konum, Vector3 olcek,
        Material mat, bool colliderKalsin = true)
    {
        return Primitif(PrimitiveType.Cube, ad, ebeveyn, konum, olcek, mat, colliderKalsin);
    }

    private static GameObject Kapsul(string ad, Transform ebeveyn, Vector3 konum, Vector3 olcek,
        Material mat, bool colliderKalsin = true)
    {
        return Primitif(PrimitiveType.Capsule, ad, ebeveyn, konum, olcek, mat, colliderKalsin);
    }

    private static GameObject Kure(string ad, Transform ebeveyn, Vector3 konum, Vector3 olcek,
        Material mat, bool colliderKalsin = true)
    {
        return Primitif(PrimitiveType.Sphere, ad, ebeveyn, konum, olcek, mat, colliderKalsin);
    }

    private static GameObject Primitif(PrimitiveType tip, string ad, Transform ebeveyn,
        Vector3 konum, Vector3 olcek, Material mat, bool colliderKalsin)
    {
        GameObject go = GameObject.CreatePrimitive(tip);
        go.name = ad;

        if (ebeveyn != null) go.transform.SetParent(ebeveyn, false);
        go.transform.localPosition = konum;
        go.transform.localScale = olcek;

        if (!colliderKalsin && go.TryGetComponent(out Collider c)) Object.DestroyImmediate(c);
        if (mat != null && go.TryGetComponent(out MeshRenderer r)) r.sharedMaterial = mat;

        return go;
    }

    private static Transform Nokta(string ad, Transform ebeveyn, Vector3 konum)
    {
        GameObject go = new GameObject(ad);
        if (ebeveyn != null) go.transform.SetParent(ebeveyn, false);
        go.transform.localPosition = konum;
        return go.transform;
    }

    private static Gradient Gradient2(Color basRenk, float basAlfa, Color sonRenk, float sonAlfa)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(basRenk, 0f), new GradientColorKey(sonRenk, 1f) },
            new[] { new GradientAlphaKey(basAlfa, 0f), new GradientAlphaKey(sonAlfa, 1f) });
        return g;
    }

    /// <summary>
    /// [SerializeField] ile gizlenmiş özel alanları editörden bağlamak için küçük yardımcı.
    /// Alan adı bulunamazsa sessiz kalmaz, Console'a uyarı basar.
    /// </summary>
    private class Baglayici
    {
        private readonly SerializedObject so;
        private readonly string hedefAdi;

        public Baglayici(Object hedef)
        {
            so = new SerializedObject(hedef);
            hedefAdi = hedef != null ? hedef.GetType().Name : "(bos)";
        }

        public Baglayici Nesne(string alan, Object deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.objectReferenceValue = deger;
            return this;
        }

        public Baglayici Sayi(string alan, float deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.floatValue = deger;
            return this;
        }

        public Baglayici TamSayi(string alan, int deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.intValue = deger;
            return this;
        }

        public Baglayici Dogru(string alan, bool deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.boolValue = deger;
            return this;
        }

        public Baglayici Metin(string alan, string deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.stringValue = deger;
            return this;
        }

        public Baglayici Numaralandirma(string alan, int deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.enumValueIndex = deger;
            return this;
        }

        public Baglayici Renk(string alan, Color deger)
        {
            SerializedProperty p = Bul(alan);
            if (p != null) p.colorValue = deger;
            return this;
        }

        public Baglayici Dizi(string alan, Object[] degerler)
        {
            SerializedProperty p = Bul(alan);
            if (p == null) return this;

            int adet = degerler != null ? degerler.Length : 0;
            p.arraySize = adet;
            for (int i = 0; i < adet; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = degerler[i];
            }
            return this;
        }

        public void Uygula()
        {
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private SerializedProperty Bul(string alan)
        {
            SerializedProperty p = so.FindProperty(alan);
            if (p == null) Debug.LogWarning("[Zar Prototip] Alan bulunamadı: " + hedefAdi + "." + alan);
            return p;
        }
    }
}
