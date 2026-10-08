import { LEGAL_DOCUMENT_VERSIONS } from "../../shared/constants/legalDocuments";
import Seo from "../../shared/seo/Seo";

const sections = [
  {
    title: "1. Az adatkezelő adatai",
    paragraphs: [
      "Adatkezelő: Bakony Kincse Biokertészet",
      "Kapcsolattartó: Volf Gábor",
      "Székhely / levelezési cím: 8431 Bakonyszentlászló, Jókai utca 22.",
      "Email: info@bakonykincse.hu",
      "Telefon: +36 20 800 56 06",
    ],
  },
  {
    title: "2. A tájékoztató célja",
    paragraphs: [
      "Jelen adatkezelési tájékoztató bemutatja, hogy a webshop használata, a regisztráció, a rendelésleadás, a kapcsolattartás és az ügyintézés során milyen személyes adatokat kezelünk, milyen célból, milyen jogalapon és mennyi ideig.",
      "A tájékoztató jelenlegi szövege előkészített, jogi ellenőrzésre váró változat. A végleges közzététel előtt ügyvédi vagy adatvédelmi szakértői ellenőrzés javasolt.",
    ],
  },
  {
    title: "3. Regisztráció és vásárlói fiók",
    paragraphs: [
      "A vásárlói fiók létrehozásához és használatához az email címet, a jelszó vissza nem fejthető ellenőrző értékét, valamint a fiókhoz később megadott kapcsolattartási és címadatokat kezeljük.",
      "Az adatkezelés célja a vásárlói fiók működtetése, a gyorsabb rendelésleadás, a rendelési előzmények megjelenítése, valamint a mentett számlázási és szállítási adatok kezelése.",
      "A fiókadatokat a felhasználói fiók fennállásáig kezeljük. A felhasználó a fiókbeállításokban kezdeményezheti a fiók deaktiválását vagy törlését, illetve a Kapcsolat menüponton keresztül is kérhet segítséget.",
    ],
  },
  {
    title: "4. Rendelés teljesítése",
    paragraphs: [
      "Rendelésleadáskor kezeljük a vásárló nevét, email címét, telefonszámát, számlázási címét, szállítási címét, a megrendelt termékeket, a rendelés értékét, a választott átvételi és fizetési módot, valamint a rendelés állapotát.",
      "Céges vásárló esetén kezeljük a cégnév, adószám, számlázási cím és nyomtatott számla kérése adatokat is. Magánszemély esetén a rendeléshez kapcsolódó nyugta kiállításához szükséges adatokat kezeljük.",
      "Az adatkezelés célja a rendelés rögzítése, visszaigazolása, teljesítése, a szállítás vagy személyes átvétel megszervezése, a számlázás vagy nyugtaadás, valamint az esetleges panaszok és garanciális ügyek kezelése.",
      "A rendeléshez kapcsolódó adatokat a számviteli, adózási, fogyasztóvédelmi és polgári jogi kötelezettségek miatt a vonatkozó jogszabályok szerinti ideig őrizzük meg.",
    ],
  },
  {
    title: "5. Fiók törlése, deaktiválása és rendelési előzmények",
    paragraphs: [
      "Deaktiválás esetén a bejelentkezési lehetőséget kikapcsoljuk, de a fiókhoz kapcsolódó adatok megmaradnak, hogy a fiók később újraaktiválható legyen. Az újraaktiválás a Kapcsolat menüponton keresztül kérhető.",
      "Ha a fiókhoz nem tartozik rendelési előzmény, törlési kérés esetén a fiókot töröljük.",
      "Ha a fiókhoz rendelési előzmény tartozik, a mentett fiókadatokat anonimizáljuk, a rendelési és számlázási adatok szükséges részét pedig megőrizzük. Erre azért van szükség, mert a korábbi rendelésekhez kapcsolódó adatok számviteli, adózási, fogyasztóvédelmi, garanciális és jogi igényérvényesítési okokból nem törölhetők teljes körűen azonnal.",
      "Aktív rendelés esetén a törlési vagy anonimizálási folyamat a rendelés lezárásáig függőben maradhat.",
    ],
  },
  {
    title: "6. Kapcsolatfelvétel",
    paragraphs: [
      "A kapcsolatfelvételi űrlapon a nevet, email címet, az opcionálisan megadott telefonszámot és tárgyat, valamint az üzenet tartalmát kezeljük. Emailben, telefonon vagy közösségi oldalon történő megkeresés esetén a megadott elérhetőséget és az ügyintézéshez szükséges információkat kezeljük.",
      "Az adatkezelés célja a kérdés, kérés, panasz vagy egyéb ügyintézés megválaszolása. Az adatokat az ügy lezárásáig, illetve jogi igény esetén a szükséges elévülési ideig őrizzük meg.",
    ],
  },
  {
    title: "7. Elállási, panasz- és fogyasztói kérések",
    paragraphs: [
      "A rendeléshez kapcsolódó elállási, panasz- vagy fogyasztói kérés rögzítésekor kezeljük a rendelés azonosítóját, a rendelésnél megadott email címet, a vásárló nevét, a kérés szövegét, a kérés státuszát, valamint a rögzítés és feldolgozás időpontját.",
      "Az adatkezelés célja a kérés azonosítása, visszaigazolása, kivizsgálása, megválaszolása és a kapcsolódó fogyasztóvédelmi vagy jogi kötelezettségek teljesítése.",
      "A kérés beérkezéséről emailes visszaigazolást küldünk. A kéréshez kapcsolódó adatokat az ügy lezárásáig, illetve jogi, fogyasztóvédelmi vagy számviteli megőrzési kötelezettség esetén a szükséges ideig őrizzük meg.",
    ],
  },
  {
    title: "8. Fizetés",
    paragraphs: [
      "Amennyiben az online bankkártyás fizetés elérhető, azt a SumUp fizetési oldalán lehet elvégezni. A bankkártya számát és biztonsági kódját nem a webshopban adod meg, és ezeket a webshop nem tárolja.",
      "A webshop a fizetési szolgáltatónak a fizetés összegét, pénznemét és a fizetéshez kapcsolódó rendelési hivatkozást továbbítja. A rendelés teljesítése és a fizetés ellenőrzése érdekében tárolja a fizetés állapotát és a szolgáltató által visszaadott fizetési azonosítókat.",
    ],
  },
  {
    title: "9. Sütik és anonim látogatottsági analitika",
    paragraphs: [
      "A bejelentkezés és a vendégkosár azonosításához szükséges sütiket használunk. A bejelentkezési munkamenet 60 perc inaktivitás után lejár; aktív használat közben meghosszabbodik. A kosár a legutóbbi lekérésétől vagy módosításától számított 30 percig érvényes. A lejárat nem jelenti valamennyi tárolt adat azonnali fizikai törlését.",
      "A pénztárban megadott kapcsolattartási, számlázási és szállítási adatokat a rendelés elküldése előtt is automatikusan mentjük a szerveren, hogy frissítés után folytatható legyen a kitöltés. A piszkozat legfeljebb az utolsó mentéstől számított 30 percig, illetve a kosár lejártáig érhető el. Kijelentkezéskor vagy a rendelés sikeres rögzítésekor a kapcsolódó piszkozatot töröljük; a lejárt piszkozatok törlését háttérfolyamat végzi. Ezeket a személyes adatokat nem mentjük a böngésző helyi tárhelyére.",
      "Az adatvédelmi beállításokat a böngésző helyi tárhelyén őrizzük meg. A választás a beállítások módosításáig, a tárolt beállítás verziójának változásáig vagy a böngészőadatok törléséig marad meg.",
      "A jelenlegi tesztváltozat nem továbbít analitikai eseményeket külső analitikai szolgáltatónak. A böngészőn belüli analitikai események csak az erre adott hozzájárulás után keletkeznek. Az analitikai események célja annak megértése, hogy mely oldalak és rendelési lépések működnek jól, illetve hol érdemes javítani a felhasználói élményt.",
      "Az analitikai események nem tartalmazhatnak nevet, email címet, telefonszámot, pontos címet, rendelésazonosítót vagy más közvetlen azonosításra alkalmas adatot.",
      "A hozzájárulás bármikor módosítható vagy visszavonható az oldal alján található Adatvédelmi beállítások menüpontban. A láblécben megjelenő Google-térkép és a szállítási hely kiválasztásához betöltött GLS-térkép külső szolgáltatás: betöltésükkor a böngésző kapcsolatba lép az adott szolgáltatóval, amely így megismerheti például az IP-címet és a böngésző technikai adatait. Ezek a kapcsolatok nem az analitikai beállítás részei.",
    ],
  },
  {
    title: "10. Adattovábbítás és adatfeldolgozók",
    paragraphs: [
      "A rendelés teljesítéséhez szükséges adatokat továbbíthatjuk könyvelési, számlázási, tárhelyszolgáltatási, informatikai, futárszolgálati vagy fizetési szolgáltató partnerek részére.",
      "Az adatfeldolgozók az adatkezelő utasításai szerint járnak el. Egyes partnerek saját szolgáltatásuk tekintetében önálló adatkezelők is lehetnek; a pontos szerepeket és az alkalmazandó tájékoztatókat a végleges szolgáltatói listában kell meghatározni.",
      "A végleges adatfeldolgozói lista a tárhely-, email-, fizetési, számlázási, szállítási és esetleges analitikai szolgáltatók kiválasztása után pontosítandó.",
    ],
  },
  {
    title: "11. Felhasználói jogok",
    paragraphs: [
      "A felhasználó kérheti a róla kezelt személyes adatokhoz való hozzáférést, azok helyesbítését, törlését, korlátozását, valamint tiltakozhat bizonyos adatkezelések ellen. A jogszabályi feltételek fennállásakor az adathordozhatósághoz való jog is megilleti. A hozzájárulás visszavonása nem érinti a visszavonás előtti adatkezelés jogszerűségét.",
      "A felhasználó jogosult panaszt tenni a Nemzeti Adatvédelmi és Információszabadság Hatóságnál, valamint bírósághoz fordulni, ha úgy véli, hogy személyes adatait jogsértően kezeljük.",
    ],
  },
  {
    title: "12. Adatbiztonság",
    paragraphs: [
      "A személyes adatok védelme érdekében technikai és szervezési intézkedéseket alkalmazunk. A felhasználói jelszavakat nem olvasható formában tároljuk.",
      "A felhasználó felelőssége, hogy fiókjához tartozó jelszavát bizalmasan kezelje, és jelezze, ha jogosulatlan hozzáférést észlel.",
    ],
  },
];

export default function PrivacyPolicyPage() {
  return (
    <div className="bg-site-surface-soft px-4 py-10 sm:py-14">
      <Seo
        title="Adatkezelési tájékoztató"
        description="A Bakony Kincse Biokertészet adatkezelési tájékoztatója regisztrációhoz, rendeléshez, kapcsolattartáshoz és fogyasztói kérésekhez."
      />
      <article className="mx-auto max-w-5xl rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-10">
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
          Jogi információk
        </p>
        <h1 className="mt-2 text-3xl font-bold text-site-heading sm:text-4xl">
          Adatkezelési tájékoztató
        </h1>
        <p className="mt-3 text-sm text-site-muted">
          Hatályos: jóváhagyás után kerül véglegesítésre. Verzió: {LEGAL_DOCUMENT_VERSIONS.privacyPolicy.replace(/^privacy-/, "")}
        </p>

        <div className="mt-8 space-y-8 text-site-text">
          {sections.map((section) => (
            <section key={section.title}>
              <h2 className="text-xl font-bold text-site-heading">{section.title}</h2>
              <div className="mt-3 space-y-3 leading-7">
                {section.paragraphs.map((paragraph) => (
                  <p key={paragraph}>{paragraph}</p>
                ))}
              </div>
            </section>
          ))}
        </div>
      </article>
    </div>
  );
}
