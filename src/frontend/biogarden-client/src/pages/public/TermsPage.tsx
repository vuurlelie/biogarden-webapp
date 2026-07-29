import { Link } from "react-router";
import { LEGAL_DOCUMENT_VERSIONS } from "../../shared/constants/legalDocuments";
import { ROUTES } from "../../shared/constants/routes";
import Seo from "../../shared/seo/Seo";

const sections = [
  {
    title: "1. Szolgáltató adatai",
    paragraphs: [
      "Szolgáltató: Bakony Kincse Biokertészet",
      "Kapcsolattartó: Volf Gábor",
      "Székhely / átvételi hely: 8431 Bakonyszentlászló, Jókai utca 22.",
      "Email: info@bakonykincse.hu",
      "Telefon: +36 20 800 56 06",
      "A végleges céges, őstermelői, nyilvántartási és adózási azonosítókat a jogi és könyvelői ellenőrzés után kell pontosítani.",
    ],
  },
  {
    title: "2. Az ÁSZF célja és hatálya",
    paragraphs: [
      "Jelen Általános Szerződési Feltételek a webshopban elérhető termékek megtekintésére, megrendelésére, átvételére, szállítására és fizetésére vonatkozó alapvető szabályokat tartalmazzák.",
      "A szöveg előkészített, jogi ellenőrzésre váró változat. A végleges közzététel előtt ügyvédi ellenőrzés javasolt, különösen az elállás, szavatosság, panaszkezelés és adatkezelés részeknél.",
    ],
  },
  {
    title: "3. Termékek és árak",
    paragraphs: [
      "A webshopban megjelenő termékek leírása, képe, kiszerelése, egységára és elérhetősége tájékoztató jellegű. Törekszünk arra, hogy minden adat pontos és naprakész legyen.",
      "A termékek ára magyar forintban értendő. Az esetleges szállítási, csomagautomatás vagy utánvételi díjak a rendelési folyamat során külön kerülnek feltüntetésre.",
      "Ha hibás ár vagy nyilvánvaló technikai hiba jelenik meg, a rendelés teljesítése előtt egyeztetünk a vásárlóval.",
    ],
  },
  {
    title: "4. Rendelés menete",
    paragraphs: [
      "A vásárló a termékeket a kosárba helyezheti, majd a pénztárban megadhatja a kapcsolattartási, számlázási és szállítási adatokat.",
      "A rendelés leadása előtt a vásárló áttekintheti a rendelés tartalmát, a választott átvételi és fizetési módot, valamint az összesített fizetendő összeget.",
      "A rendelés elküldésével a vásárló ajánlatot tesz a kiválasztott termékek megvásárlására. A rendelés beérkezéséről a rendszer visszajelzést ad, a teljesítés részleteiről szükség esetén külön egyeztetünk.",
      "A rendelés leadása előtt a vásárló számára megjelenik a megrendelés összesítője, a fizetendő végösszeg, az átvételi vagy szállítási mód, a fizetési mód, valamint a magánszemélyeknek vagy cégeknek kiállítandó bizonylat típusa.",
    ],
  },
  {
    title: "5. Vásárlói fiók",
    paragraphs: [
      "A vásárló regisztrált fiókot hozhat létre, amely megkönnyíti a rendelésleadást, a címadatok kezelését és a korábbi rendelések áttekintését.",
      "A vásárló köteles valós, pontos és naprakész adatokat megadni. A hibás vagy hiányos adatokból eredő késedelemért vagy sikertelen teljesítésért a szolgáltató nem vállal felelősséget.",
      "A fiók deaktiválása vagy törlése a Kapcsolat menüponton keresztül kérhető. Rendelési előzmény esetén a jogszabályi megőrzési kötelezettségek miatt egyes rendelési és számlázási adatok továbbra is megőrzésre kerülhetnek.",
    ],
  },
  {
    title: "6. Átvétel és szállítás",
    paragraphs: [
      "A rendelés személyes átvétellel, házhoz szállítással vagy csomagautomatás szállítással teljesíthető, ha az adott termék és rendelés esetében ezek a módok elérhetők.",
      "Személyes átvétel esetén az átvételi időpontot és az átvétel részleteit a megadott elérhetőségeken egyeztetjük.",
      "Szállítás esetén a vásárló felelőssége, hogy pontos és teljes szállítási címet adjon meg. A szállítási díj és az esetleges utánvételi díj a pénztárban látható.",
      "A termékek jellegére tekintettel a szállíthatóság, a szállítási határidő és az átvételi lehetőség termékenként eltérhet; a nem szállítható termékeknél a rendelési folyamat személyes átvételre korlátozható.",
    ],
  },
  {
    title: "7. Fizetési módok",
    paragraphs: [
      "A webshopban elérhető fizetési módok a rendelési folyamatban jelennek meg. Személyes átvétel esetén készpénzes vagy helyszíni bankkártyás fizetés is elérhető lehet.",
      "Utánvét esetén a vásárló a csomag átvételekor fizet. Online bankkártyás fizetés bevezetése esetén a kártyaadatokat külső, biztonságos fizetési szolgáltató kezeli, a webshop saját szervere nem tárol bankkártyaadatokat.",
      "A rendelés teljesítése a fizetési mód és a rendelés állapota alapján történik.",
      "Magánszemély vásárló részére a rendeléshez papíralapú nyugtát mellékelünk. Céges vásárló részére számla készül; alapértelmezetten elektronikus számla, külön kérés esetén nyomtatott számla is mellékelhető. A pontos bizonylatolási gyakorlatot a könyvelői ellenőrzés véglegesíti.",
    ],
  },
  {
    title: "8. Elállás és lemondás",
    paragraphs: [
      "A vásárlót a vonatkozó jogszabályok szerint elállási jog illetheti meg. Romlandó, minőségét rövid ideig megőrző, egyedi kérésre készített vagy higiéniai okból nem visszaküldhető termékek esetében az elállási jog korlátozott vagy kizárt lehet.",
      "Az elállási, panasz- vagy fogyasztói kérés online is rögzíthető a rendelés azonosítója és a rendelésnél megadott email cím alapján. A kérés beérkezését emailben visszaigazoljuk.",
      "Aktív rendelés lemondását a lehető leghamarabb jelezni kell a Kapcsolat menüponton megadott elérhetőségek egyikén vagy a rendelési felületen.",
      "A már előkészített, feladott vagy átvételre kész rendelés lemondása esetén a felmerült költségek rendezése külön egyeztetést igényelhet.",
      "A termékkörre vonatkozó elállási kivételek és határidők végleges szövegét ügyvédi ellenőrzés után kell közzétenni.",
    ],
  },
  {
    title: "9. Szavatosság, minőségi kifogás",
    paragraphs: [
      "A vásárló minőségi kifogását a hiba észlelését követően mielőbb jelezheti emailben vagy telefonon.",
      "A bejelentéshez kérhetjük a rendelés azonosítóját, a termék megnevezését, a hiba leírását és szükség esetén fényképet. A panaszokat egyedileg vizsgáljuk ki.",
      "A szavatossági és fogyasztóvédelmi jogok gyakorlására a mindenkor hatályos jogszabályok irányadók.",
    ],
  },
  {
    title: "10. Panaszkezelés",
    paragraphs: [
      "A vásárló panaszát az info@bakonykincse.hu email címen vagy telefonon jelezheti.",
      "A panaszokat igyekszünk békésen, gyorsan és átláthatóan rendezni. Írásbeli panasz esetén a választ a jogszabályi határidők figyelembevételével küldjük meg.",
      "A vásárló fogyasztói jogvita esetén a lakóhelye vagy tartózkodási helye szerint illetékes békéltető testülethez is fordulhat. A közzéteendő békéltető testületi és hatósági adatok véglegesítését jogi ellenőrzéshez kötjük.",
      "A panasz bejelentésekor kérhetjük a rendelés azonosítóját, a kapcsolattartási adatokat, a kifogás rövid leírását és szükség esetén a kapcsolódó dokumentumokat vagy fényképeket.",
    ],
  },
  {
    title: "11. Felelősség",
    paragraphs: [
      "A webshop működtetése során törekszünk a pontos, biztonságos és folyamatos működésre, de technikai hiba, karbantartás vagy külső szolgáltatói probléma előfordulhat.",
      "Nem vállalunk felelősséget olyan késedelemért vagy hibáért, amely a vásárló által megadott pontatlan adatokból, elérhetetlenségből vagy külső szolgáltató hibájából ered.",
    ],
  },
  {
    title: "12. Záró rendelkezések",
    paragraphs: [
      "A szolgáltató jogosult az ÁSZF módosítására. A módosított feltételek a webshopban történő közzétételtől alkalmazandók.",
      "A jelen ÁSZF-ben nem szabályozott kérdésekben a magyar jog, különösen a polgári jogi, fogyasztóvédelmi, elektronikus kereskedelmi és adatvédelmi szabályok irányadók.",
    ],
  },
];

export default function TermsPage() {
  return (
    <main className="bg-site-surface-soft px-4 py-10 sm:py-14">
      <Seo
        title="Általános Szerződési Feltételek"
        description="A Bakony Kincse Biokertészet rendelési, fizetési, átvételi, panaszkezelési és fogyasztói tájékoztatói."
      />
      <article className="mx-auto max-w-5xl rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-10">
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
          Jogi információk
        </p>
        <h1 className="mt-2 text-3xl font-bold text-site-heading sm:text-4xl">
          Általános Szerződési Feltételek
        </h1>
        <p className="mt-3 text-sm text-site-muted">
          Hatályos: jóváhagyás után kerül véglegesítésre. Verzió: {LEGAL_DOCUMENT_VERSIONS.terms}
        </p>
        <div className="mt-5 rounded-sm border border-site-green/25 bg-site-surface-soft p-4 text-sm leading-6 text-site-text">
          <p>
            Ez a szöveg technikai előkészítés és jogi-könyvelői ellenőrzésre vár. A rendeléshez kapcsolódó elállási,
            panasz- vagy fogyasztói kérés külön oldalon is rögzíthető:{" "}
            <Link to={ROUTES.orderWithdrawalRequest} className="font-semibold text-site-green-dark underline">
              elállási vagy fogyasztói kérés
            </Link>
            .
          </p>
        </div>

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
