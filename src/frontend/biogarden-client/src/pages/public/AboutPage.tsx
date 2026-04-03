import AboutGarden from "../../features/about/components/AboutGarden";
import AboutItems from "../../features/about/components/AboutItems";

import bemutatkozo from "../../assets/images/bemutatkozo.png";
import bemutatokep from "../../assets/images/bemutatokep.jpg";

export default function AboutPage() {
  return (
    <>
      <AboutGarden bemutatkozo={bemutatkozo} />
      <AboutItems bemutatokep={bemutatokep} />
    </>
  );
}