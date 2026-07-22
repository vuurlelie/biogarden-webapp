import Carousel from "../../features/home/components/Carousel";
import ArticleGrid from "../../features/home/components/ArticleGrid";
import Seo from "../../shared/seo/Seo";

import firstslide from "../../assets/images/firstcarousel.jpg";
import secondslide from "../../assets/images/secondcarousel.jpg";
import thirdslide from "../../assets/images/thirdcarousel.jpg";
import fourthslide from "../../assets/images/fourthcarousel.jpg";
import actual from "../../assets/images/actual.jpg";

const slides = [firstslide, secondslide, thirdslide, fourthslide];

export default function HomePage() {
  return (
    <>
      <Seo
        description="Bakony Kincse Biokertészet: természetes termékek, gyógynövények, teák, illóolajok és hidrolátumok Bakonyszentlászlóról."
      />
      {/* CAROUSEL */}
      <div className="w-full px-0 mt-2 sm:mt-3 shadow-lg">
        <Carousel auto interval={5000}>
          {slides.map((src, i) => (
            <img
              key={i}
              src={src}
              alt={`Biogarden slide ${i + 1}`}
              className="w-full object-cover object-center"
              loading={i > 0 ? "lazy" : "eager"}
              decoding="async"
            />
          ))}
        </Carousel>
      </div>

      {/* ARTICLES */}
      <div className="max-w-6xl mx-auto px-4">
        <ArticleGrid />
      </div>

      {/* BANNER */}
      <div className="w-full px-0 shadow-lg">
        <img
          className="w-full h-auto"
          src={actual}
          alt="Aktuális hirdetés"
          loading="lazy"
          decoding="async"
        />
      </div>
    </>
  );
}
