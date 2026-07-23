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
      "A vásárlói fiók létrehozásához és használatához az email címet, a jelszó technikai azonosítóját, valamint a fiókhoz később megadott kapcsolattartási és címadatokat kezeljük.",
      "Az adatkezelés célja a vásárlói fiók működtetése, a gyorsabb rendelésleadás, a rendelési előzmények megjelenítése, valamint a mentett számlázási és szállítási adatok kezelése.",
      "A fiókadatokat a felhasználói fiók fennállásáig kezeljük. A felhasználó kérheti a fiók deaktiválását vagy törlését a Kapcsolat menüponton keresztül.",
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
      "Emailben, telefonon vagy közösségi oldalon történő megkeresés esetén a megadott nevet, elérhetőséget és az üzenet tartalmát kezeljük.",
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
      "A webshopban jelenleg a tesztelési folyamat részeként több fizetési mód jelenhet meg. Online bankkártyás fizetés bevezetése esetén a kártyaadatokat nem a webshop szervere kezeli, hanem a kiválasztott fizetési szolgáltató biztonságos rendszere.",
      "A webshop a fizetés állapotát, a rendelés azonosítóját és a fizetéshez szükséges technikai információkat kezelheti a rendelés teljesítése és ellenőrzése érdekében.",
    ],
  },
  {
    title: "9. Sütik és anonim látogatottsági analitika",
    paragraphs: [
      "A weboldal működéséhez szükséges technikai adatokat és helyi böngészőtárhelyet a szolgáltatás biztonságos működtetéséhez használjuk.",
      "Anonim látogatottsági analitikát kizárólag előzetes hozzájárulás alapján használunk. Az analitikai események célja annak megértése, hogy mely oldalak és rendelési lépések működnek jól, illetve hol érdemes javítani a felhasználói élményt.",
      "Az analitikai események nem tartalmazhatnak nevet, email címet, telefonszámot, pontos címet, rendelésazonosítót vagy más közvetlen azonosításra alkalmas adatot.",
      "A hozzájárulás bármikor módosítható vagy visszavonható az oldal alján található Adatvédelmi beállítások menüpontban.",
    ],
  },
  {
    title: "10. Adattovábbítás és adatfeldolgozók",
    paragraphs: [
      "A rendelés teljesítéséhez szükséges adatokat továbbíthatjuk könyvelési, számlázási, tárhelyszolgáltatási, informatikai, futárszolgálati vagy fizetési szolgáltató partnerek részére.",
      "Az adatfeldolgozók kizárólag az adatkezelő utasításai szerint, a szolgáltatás teljesítéséhez szükséges mértékben kezelhetik az adatokat.",
      "A végleges adatfeldolgozói lista a tárhely-, email-, fizetési, számlázási, szállítási és esetleges analitikai szolgáltatók kiválasztása után pontosítandó.",
    ],
  },
  {
    title: "11. Felhasználói jogok",
    paragraphs: [
      "A felhasználó kérheti a róla kezelt személyes adatokhoz való hozzáférést, azok helyesbítését, törlését, korlátozását, valamint tiltakozhat bizonyos adatkezelések ellen.",
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
    <main className="bg-site-surface-soft px-4 py-10 sm:py-14">
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
          Hatályos: jóváhagyás után kerül véglegesítésre. Verzió: {LEGAL_DOCUMENT_VERSIONS.privacyPolicy}
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
    </main>
  );
}
