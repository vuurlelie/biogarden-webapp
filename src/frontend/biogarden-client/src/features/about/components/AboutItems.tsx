export default function AboutItems({ bemutatokep }: { bemutatokep: string }) {
  return (
    <section id="about" className="relative mt-16 overflow-hidden bg-white shadow-lg">
      <div className="relative max-w-6xl mx-auto">
        <div className="relative z-10 pb-10 bg-white lg:max-w-2xl lg:w-full">
          <svg
            className="hidden lg:block absolute right-0 inset-y-0 h-full w-48 text-white translate-x-1/2"
            fill="currentColor"
            viewBox="0 0 100 100"
            aria-hidden="true"
          />

          <div className="px-4">
            <div className="text-left">
              <h2 className="mb-6 text-xl md:text-2xl font-bold text-site-heading">
                Fenntartható gazdálkodás
              </h2>

              <ul className="max-w-prose space-y-2 text-site-text list-disc list-inside text-justify">
                <li>Alapelvünk a fenntarthatóság és a biodiverzitás megőrzése.</li>
                <li>Minden zöldséget, gyógy- és fűszernövényt magunk termesztünk a birtokon.</li>
                <li>Kizárólag természetes növényvédelmi és növénytáplálási megoldásokat alkalmazunk – ez egyszerre óvja a környezetet és garantálja a termékeink magas minőségét.</li>
                <li>Főbb növényeink közé tartozik a citromfű, rozmaring, fodor- és borsmenta, bazsalikom, oregánó, petrezselyem és a kakukkfű.</li> 
                <li>A kézi szedést követően a szárítást természetes módon végezzük, majd a feldolgozás és a csomagolás is házon belül történik.</li>
                <li>A megrendelt zöldségeket mindig frissen szedjük, szezonális kínálatunk folyamatosan bővül. Minőségükre teljes garanciát vállalunk.</li>
              </ul>

              <h3 className="mt-8 mb-4 text-xl md:text-2xl font-bold text-site-heading">
                Feldolgozott termékeink nem tartalmaznak:
              </h3>
              <ul className="max-w-prose space-y-1 text-site-text list-disc list-inside">
                <li>Hozzáadott sót</li>
                <li>Mesterséges adalékanyagokat</li>
                <li>Tartósítószereket</li>
                <li>Színezékeket</li>
              </ul>
            </div>
          </div>
        </div>

        <div className="px-4 lg:absolute lg:inset-y-0 lg:right-4 lg:w-[calc(50%-1rem)] lg:px-0">
          <img
            className="h-auto w-full object-cover object-top sm:h-72 md:h-96 lg:h-full"
            src={bemutatokep}
            width={1220}
            height={1251}
            alt="Kertészeti életkép"
            loading="lazy"
            decoding="async"
          />
        </div>
      </div>
    </section>
  );
}
