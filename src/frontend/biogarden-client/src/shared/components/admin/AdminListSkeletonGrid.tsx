type AdminListSkeletonGridProps = {
  gridClassName?: string;
  itemHeightClassName?: string;
};

export default function AdminListSkeletonGrid({
  gridClassName = "grid gap-5 md:grid-cols-2 xl:grid-cols-3",
  itemHeightClassName = "h-80",
}: AdminListSkeletonGridProps) {
  return (
    <div className={gridClassName}>
      {Array.from({ length: 6 }, (_, index) => (
        <div
          key={index}
          className={`${itemHeightClassName} animate-pulse rounded-sm border border-site-border bg-white`}
        />
      ))}
    </div>
  );
}
