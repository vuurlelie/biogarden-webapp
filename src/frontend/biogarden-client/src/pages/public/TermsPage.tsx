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
      "A termékoldalakon megtalálható a termék leírása, képe, kiszerelése, ára és aktuális elérhetősége. A rendelés elküldése előtt a rendszer ismét ellenőrzi a készletet és a fizetendő összeget.",
      "Az árak magyar forintban értendők. A pénztár külön feltünteti a termékek összegét, a szállítás díját, az esetleges utánvételi díjat és a fizetendő végösszeget. A tesztváltozat szállítási díjai előzetes értékek; a végleges díjszabás és az árak adózási tartalma szolgáltatói és könyvelői jóváhagyásra vár.",
      "Ha hibás ár vagy nyilvánvaló technikai hiba jelenik meg, a rendelés teljesítése előtt egyeztetünk a vásárlóval.",
    ],
  },
  {
    title: "4. Rendelés menete",
    paragraphs: [
      "Rendelés vendégként vagy bejelentkezett vásárlóként is leadható. A vásárló a termékeket a kosárba helyezi, majd a pénztárban megadja a kapcsolattartási, számlázási és szállítási adatokat. A kosár a legutóbbi lekérésétől vagy módosításától számított 30 percig érvényes; lejárat után a termékeket ismét a kosárba kell helyezni.",
      "A végleges elküldés előtt megjelenő összesítőn ellenőrizhető a rendelés tartalma, az átvételi és fizetési mód, a megadott adatok és a fizetendő végösszeg. Az összesítő bezárásával az adatok javíthatók. Ha időközben változik az ár, a díj vagy a kiválasztott átvételi hely, a rendszer új áttekintést kér.",
      "A rendelés elküldésével a vásárló ajánlatot tesz a kiválasztott termékek megvásárlására. A rendszer a sikeres rögzítést a képernyőn jelzi, és emailes visszaigazolást küld. Az email megérkezése nem feltétele a rendelés technikai rögzítésének; késés esetén ne adj le automatikusan új rendelést, hanem ellenőrizd a rendelés állapotát vagy lépj kapcsolatba velünk.",
      "A szerződés létrejöttének pontos időpontját, az automatikus visszaigazolás jogi szerepét és a rendelés elfogadásának rendjét a végleges ÁSZF-ben kell rögzíteni. A visszaigazolás késedelméhez kapcsolódó jogszabályi jogokat a technikai működés nem korlátozza.",
    ],
  },
  {
    title: "5. Vásárlói fiók",
    paragraphs: [
      "A vásárló regisztrált fiókot hozhat létre, amely megkönnyíti a rendelésleadást, a címadatok kezelését és a korábbi rendelések áttekintését.",
      "A vásárló köteles valós, pontos és naprakész adatokat megadni. A hibás vagy hiányos adatokból eredő késedelemért vagy sikertelen teljesítésért a szolgáltató nem vállal felelősséget.",
      "A fiók deaktiválása vagy törlése a fiókbeállításokban kezdeményezhető, illetve a Kapcsolat menüponton keresztül is kérhető segítség. Rendelési előzmény esetén a jogszabályi megőrzési kötelezettségek miatt egyes rendelési és számlázási adatok továbbra is megőrzésre kerülhetnek.",
    ],
  },
  {
    title: "6. Átvétel és szállítás",
    paragraphs: [
      "A pénztár az adott rendeléshez elérhető átvételi módokat kínálja fel: személyes átvételt, házhoz szállítást, GLS csomagautomatát vagy GLS CsomagPontot. A csomagautomatát és a CsomagPontot külön lehet kiválasztani; az átvételi helyet a beépített térképen kell megjelölni, majd ellenőrizni a megjelenő nevet és címet.",
      "Személyes átvétel esetén az átvételi időpontot és az átvétel részleteit a megadott elérhetőségeken egyeztetjük.",
      "Szállítás esetén a vásárló felelőssége, hogy pontos és teljes szállítási címet adjon meg. A szállítási díj és az esetleges utánvételi díj a pénztárban látható.",
      "A termékek jellegére tekintettel a szállíthatóság, a szállítási határidő és az átvételi lehetőség termékenként eltérhet; a nem szállítható termékeknél a rendelési folyamat személyes átvételre korlátozható.",
    ],
  },
  {
    title: "7. Fizetési módok",
    paragraphs: [
      "A rendeléshez választható fizetési módokat a pénztár mutatja. Személyes átvételnél online bankkártyás vagy átvételkori fizetés, szállításnál online bankkártyás vagy utánvétes fizetés jelenhet meg, az engedélyezett lehetőségek szerint. Az átvételkori fizetés pontos módját előzetesen egyeztetni kell.",
      "Utánvét esetén a vásárló az átvételhez kapcsolódóan fizet; a díjat a pénztár külön jelzi. GLS csomagautomatánál vagy CsomagPontnál utánvét csak akkor választható, ha azt a kiválasztott hely támogatja. Online bankkártyás fizetés esetén a vásárló a SumUp fizetési oldalára kerül; a webshop nem tárolja a bankkártya számát vagy biztonsági kódját.",
      "Az online fizetés eredményét a webshop a fizetési szolgáltatónál ellenőrzi; a fizetési oldalról történő visszatérés önmagában nem igazolja a sikeres fizetést. Megszakadt kapcsolat vagy bizonytalan fizetési állapot esetén először a meglévő rendelés állapotát kell ellenőrizni. A rendelés teljesítése a fizetési mód és az ellenőrzött rendelési állapot alapján történik.",
      "A pénztár rögzíti, hogy magánszemélyként vagy cégként történik a vásárlás, továbbá a céges számlázási adatokat és a nyomtatott számla iránti igényt. A bizonylat típusát, kiállításának és átadásának rendjét a könyvelői ellenőrzés alapján kell véglegesíteni; a webshop önmagában nem állít ki adóügyi számlát.",
    ],
  },
  {
    title: "8. Elállás és lemondás",
    paragraphs: [
      "Fogyasztói távértékesítésnél főszabály szerint az áru átvételétől számított 14 napon belül indokolás nélkül lehet elállni, és a jog a szerződés megkötése és az átvétel között is gyakorolható. A jogszabály szerinti kivételeket termékenként kell megvizsgálni: ide tartozhat például a romlandó vagy rövid ideig eltartható áru, az egyedileg készített áru, illetve a felbontott, zárt csomagolású áru, ha egészségvédelmi vagy higiéniai okból nem küldhető vissza. Nem minden élelmiszer vagy kozmetikum esik automatikusan kivétel alá.",
      "Az elállási, panasz- vagy fogyasztói kérés online is rögzíthető a rendelés azonosítója és a rendelésnél megadott email cím alapján. A kérés beérkezését emailben visszaigazoljuk.",
      "Aktív rendelés lemondását a lehető leghamarabb jelezni kell a Kapcsolat menüponton megadott elérhetőségek egyikén vagy a rendelési felületen.",
      "Az online kérés rögzítése nem jelent automatikus pénzvisszatérítést. A jogosultságot és a visszatérítés módját ügyintézés során ellenőrizzük, a kötelező jogszabályi határidők betartásával. A feladás vagy előkészítés ténye önmagában nem zárja ki a jogszerű fogyasztói elállást.",
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
      "Pontatlan adatok, elérhetetlenség vagy szolgáltatási hiba esetén a teljesítés lehetőségeit a vásárlóval egyeztetjük. Ez nem korlátozza a szolgáltató jogszabályon alapuló felelősségét vagy a fogyasztó kötelező jogait.",
    ],
  },
  {
    title: "12. Záró rendelkezések",
    paragraphs: [
      "Az ÁSZF módosulhat; a rendeléshez a leadáskor elfogadott változat azonosítóját rögzítjük. Egy későbbi módosítás önmagában nem írja át a korábbi rendelés feltételeit.",
      "A jelen ÁSZF-ben nem szabályozott kérdésekben a magyar jog, különösen a polgári jogi, fogyasztóvédelmi, elektronikus kereskedelmi és adatvédelmi szabályok irányadók.",
    ],
  },
];

export default function TermsPage() {
  return (
    <div className="bg-site-surface-soft px-4 py-10 sm:py-14">
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
          Hatályos: jóváhagyás után kerül véglegesítésre. Verzió: {LEGAL_DOCUMENT_VERSIONS.terms.replace(/^aszf-/, "")}
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
    </div>
  );
}
