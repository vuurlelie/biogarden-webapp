import AboutGarden from "../../features/about/components/AboutGarden";
import AboutItems from "../../features/about/components/AboutItems";
import Seo from "../../shared/seo/Seo";

import bemutatkozo from "../../assets/images/bemutatkozo.jpg";
import bemutatokep from "../../assets/images/bemutatokep.jpg";

export default function AboutPage() {
  return (
    <>
      <Seo
        title="Rólunk"
        description="Ismerd meg a Bakony Kincse Biokertészet szemléletét, kertjét és természetes termékeit Bakonyszentlászlón."
      />
      <AboutGarden bemutatkozo={bemutatkozo} />
      <AboutItems bemutatokep={bemutatokep} />
    </>
  );
}
