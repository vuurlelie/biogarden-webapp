import { Extension } from "@tiptap/core";
import Color from "@tiptap/extension-color";
import FontFamily from "@tiptap/extension-font-family";
import Link from "@tiptap/extension-link";
import TextAlign from "@tiptap/extension-text-align";
import { TextStyle } from "@tiptap/extension-text-style";
import Underline from "@tiptap/extension-underline";
import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import { useEffect, useState } from "react";

type RichTextEditorProps = {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  error?: string;
};

const fontFamilies = [
  ["", "Alapértelmezett"],
  ["Arial", "Arial"],
  ["Georgia", "Georgia"],
  ["Times New Roman", "Times New Roman"],
  ["Verdana", "Verdana"],
  ["Segoe UI", "Segoe UI"],
];

const fontSizes = [
  ["", "Normál"],
  ["0.875rem", "Kicsi"],
  ["1rem", "Normál"],
  ["1.125rem", "Nagy"],
  ["1.35rem", "Nagyon nagy"],
  ["1.6rem", "Címszerű"],
];

const FontSize = Extension.create({
  name: "fontSize",

  addGlobalAttributes() {
    return [
      {
        types: ["textStyle"],
        attributes: {
          fontSize: {
            default: null,
            parseHTML: (element) => element.style.fontSize || null,
            renderHTML: (attributes) => {
              if (!attributes.fontSize)
                return {};
              return { style: `font-size: ${attributes.fontSize}` };
            },
          },
        },
      },
    ];
  },

  addCommands() {
    return {
      setFontSize:
        (fontSize: string) =>
        ({ chain }) =>
          chain().setMark(
            "textStyle",
            { fontSize }
          ).run(),
      unsetFontSize:
        () =>
        ({ chain }) =>
          chain().setMark(
            "textStyle",
            { fontSize: null }
          ).removeEmptyTextStyle().run(),
    };
  },
});

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    fontSize: {
      setFontSize: (fontSize: string) => ReturnType;
      unsetFontSize: () => ReturnType;
    };
  }
}

function ToolbarDivider() {
  return <span className="mx-1 hidden h-7 w-px bg-site-border sm:inline-block" aria-hidden="true" />;
}

export default function RichTextEditor({
  id,
  label,
  value,
  onChange,
  required = false,
  error,
}: RichTextEditorProps) {
  const [textColor, setTextColor] = useState("#243127");
  const [editorStateVersion, setEditorStateVersion] = useState(0);

  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({
        link: false,
      }),
      TextStyle,
      FontFamily,
      FontSize,
      Color,
      Underline,
      TextAlign.configure({
        types: ["heading", "paragraph"],
      }),
      Link.configure({
        openOnClick: false,
        autolink: true,
        defaultProtocol: "https",
      }),
    ],
    content: value,
    editorProps: {
      attributes: {
        id,
        class:
          "rich-text-content min-h-80 max-w-none overflow-y-auto bg-white px-5 py-4 leading-7 text-site-text outline-none focus:ring-2 focus:ring-site-green/20",
        "aria-required": String(required),
        "aria-invalid": String(Boolean(error)),
        ...(error ? { "aria-describedby": `${id}-error` } : {}),
      },
    },
    onUpdate: ({ editor: updatedEditor }) => {
      onChange(updatedEditor.getHTML());
    },
  });

  useEffect(
    () => {
      if (!editor)
        return;
      if (value !== editor.getHTML()) {
        editor.commands.setContent(
          value,
          { emitUpdate: false }
        );
      }
    },
    [editor, value]
  );

  useEffect(
    () => {
      if (!editor)
        return;

      const refreshToolbarState = () => setEditorStateVersion((version) => version + 1);
      editor.on("transaction", refreshToolbarState);
      editor.on("selectionUpdate", refreshToolbarState);
      editor.on("update", refreshToolbarState);

      return () => {
        editor.off("transaction", refreshToolbarState);
        editor.off("selectionUpdate", refreshToolbarState);
        editor.off("update", refreshToolbarState);
      };
    },
    [editor]
  );

  const currentBlock = (() => {
    void editorStateVersion;
    if (!editor)
      return "paragraph";

    if (editor.isActive(
      "heading",
      { level: 2 }
    ))
      {
      return "h2";
    }


    if (editor.isActive(
      "heading",
      { level: 3 }
    ))
      {
      return "h3";
    }


    if (editor.isActive(
      "heading",
      { level: 4 }
    ))
      {
      return "h4";
    }

    return "paragraph";
  })();

  if (!editor) {
    return (
      <div>
        <label htmlFor={id} className="block text-sm font-semibold">
          {label} {required && <span className="text-site-danger">*</span>}
        </label>
        <div className="mt-1.5 h-96 animate-pulse rounded-sm border border-site-border bg-site-surface-soft" />
      </div>
    );
  }

  function setLink() {
    if (!editor)
      return;
    const previousUrl = editor.getAttributes("link").href as string | undefined;
    const url = window.prompt("Add meg a link URL-jét:", previousUrl ?? "");

    if (url === null)
      return;
    if (url.trim() === "") {
      editor.chain().focus().extendMarkRange("link").unsetLink().run();
      return;
    }

    editor.chain().focus().extendMarkRange("link").setLink({ href: url.trim() }).run();
  }

  const toolbarButtonClass =
    "grid h-9 min-w-9 place-items-center rounded-sm border border-transparent px-2 text-sm font-semibold text-site-muted transition hover:border-site-border hover:bg-white hover:text-site-heading focus:outline-none focus:ring-2 focus:ring-site-green/20 disabled:cursor-not-allowed disabled:opacity-40";

  const toolbarSelectClass =
    "h-9 rounded-sm border border-transparent bg-transparent px-2 text-sm font-semibold text-site-heading transition hover:border-site-border hover:bg-white focus:border-site-green focus:bg-white focus:outline-none";

  const activeButtonClass = "border-site-border bg-white text-site-heading shadow-sm";

  return (
    <div>
      <label htmlFor={id} className="block text-sm font-semibold">
        {label} {required && <span className="text-site-danger">*</span>}
      </label>

      <div
        className={`mt-1.5 overflow-hidden rounded-sm border bg-white shadow-sm ${
          error ? "border-site-danger" : "border-site-border"
        }`}
      >
        <div
          className="border-b border-site-border bg-[#f7f8f5]"
          onMouseDown={(event) => {
            if ((event.target as HTMLElement).closest("button"))
              event.preventDefault();
          }}
        >
          <div className="flex flex-wrap items-center gap-1 px-3 py-2">
            <button
              type="button"
              title="Félkövér"
              aria-label="Félkövér"
              className={`${toolbarButtonClass} ${editor.isActive("bold") ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().toggleBold().run()}
            >
              <span className="font-black">B</span>
            </button>
            <button
              type="button"
              title="Dőlt"
              aria-label="Dőlt"
              className={`${toolbarButtonClass} ${editor.isActive("italic") ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().toggleItalic().run()}
            >
              <span className="font-serif italic">I</span>
            </button>
            <button
              type="button"
              title="Aláhúzott"
              aria-label="Aláhúzott"
              className={`${toolbarButtonClass} ${editor.isActive("underline") ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().toggleUnderline().run()}
            >
              <span className="underline">U</span>
            </button>
            <button
              type="button"
              title="Áthúzott"
              aria-label="Áthúzott"
              className={`${toolbarButtonClass} ${editor.isActive("strike") ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().toggleStrike().run()}
            >
              <span className="line-through">S</span>
            </button>

            <ToolbarDivider />

            <select
              aria-label="Betűtípus"
              className={`${toolbarSelectClass} w-36`}
              defaultValue=""
              onChange={(event) => {
                const nextFont = event.target.value;
                if (nextFont)
                  editor.chain().focus().setFontFamily(nextFont).run();
                else
                  editor.chain().focus().unsetFontFamily().run();
              }}
            >
              {fontFamilies.map(([optionValue, optionLabel]) => (
                <option key={optionValue} value={optionValue}>{optionLabel}</option>
              ))}
            </select>

            <select
              aria-label="Betűméret"
              className={`${toolbarSelectClass} w-28`}
              defaultValue=""
              onChange={(event) => {
                const nextSize = event.target.value;
                if (nextSize)
                  editor.chain().focus().setFontSize(nextSize).run();
                else
                  editor.chain().focus().unsetFontSize().run();
              }}
            >
              {fontSizes.map(([optionValue, optionLabel]) => (
                <option key={optionValue} value={optionValue}>{optionLabel}</option>
              ))}
            </select>

            <label
              title="Szövegszín"
              className="relative grid h-9 min-w-9 cursor-pointer place-items-center rounded-sm border border-transparent px-2 text-sm font-semibold text-site-muted transition hover:border-site-border hover:bg-white hover:text-site-heading"
            >
              <span className="relative">
                A
                <span className="absolute -bottom-1 left-0 h-0.5 w-full rounded-full" style={{ backgroundColor: textColor }} />
              </span>
              <input
                type="color"
                value={textColor}
                aria-label="Szövegszín"
                onChange={(event) => {
                  setTextColor(event.target.value);
                  editor.chain().focus().setColor(event.target.value).run();
                }}
                className="absolute inset-0 cursor-pointer opacity-0"
              />
            </label>

            <ToolbarDivider />

            <button
              type="button"
              title="Formázás törlése"
              aria-label="Formázás törlése"
              className={toolbarButtonClass}
              onClick={() => editor.chain().focus().unsetAllMarks().clearNodes().run()}
            >
              T×
            </button>
          </div>

          <div className="flex flex-wrap items-center gap-1 border-t border-site-border/70 px-3 py-2">
            <select
              aria-label="Szövegstílus"
              className={`${toolbarSelectClass} w-36`}
              value={currentBlock}
              onChange={(event) => {
                const block = event.target.value;
                if (block === "h2")
                  editor.chain().focus().setHeading({ level: 2 }).run();
                else if (block === "h3")
                  editor.chain().focus().setHeading({ level: 3 }).run();
                else if (block === "h4")
                  editor.chain().focus().setHeading({ level: 4 }).run();
                else
                  editor.chain().focus().setParagraph().run();
              }}
            >
              <option value="paragraph">Bekezdés</option>
              <option value="h2">Címsor 2</option>
              <option value="h3">Címsor 3</option>
              <option value="h4">Címsor 4</option>
            </select>

            <ToolbarDivider />

            <button
              type="button"
              title="Balra igazítás"
              aria-label="Balra igazítás"
              className={`${toolbarButtonClass} ${editor.isActive({ textAlign: "left" }) ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().setTextAlign("left").run()}
            >
              ≡
            </button>
            <button
              type="button"
              title="Középre igazítás"
              aria-label="Középre igazítás"
              className={`${toolbarButtonClass} ${editor.isActive({ textAlign: "center" }) ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().setTextAlign("center").run()}
            >
              ≣
            </button>
            <button
              type="button"
              title="Jobbra igazítás"
              aria-label="Jobbra igazítás"
              className={`${toolbarButtonClass} ${editor.isActive({ textAlign: "right" }) ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().setTextAlign("right").run()}
            >
              ≡›
            </button>
            <button
              type="button"
              title="Sorkizárt"
              aria-label="Sorkizárt"
              className={`${toolbarButtonClass} ${editor.isActive({ textAlign: "justify" }) ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().setTextAlign("justify").run()}
            >
              ☰
            </button>

            <ToolbarDivider />

            <button
              type="button"
              title="Felsorolás"
              aria-label="Felsorolás"
              className={`${toolbarButtonClass} ${editor.isActive("bulletList") ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().toggleBulletList().run()}
            >
              •≡
            </button>
            <button
              type="button"
              title="Számozás"
              aria-label="Számozás"
              className={`${toolbarButtonClass} ${editor.isActive("orderedList") ? activeButtonClass : ""}`}
              onClick={() => editor.chain().focus().toggleOrderedList().run()}
            >
              1≡
            </button>
            <button
              type="button"
              title="Behúzás csökkentése"
              aria-label="Behúzás csökkentése"
              className={toolbarButtonClass}
              onClick={() => editor.chain().focus().liftListItem("listItem").run()}
              disabled={!editor.can().liftListItem("listItem")}
            >
              ‹≡
            </button>
            <button
              type="button"
              title="Behúzás növelése"
              aria-label="Behúzás növelése"
              className={toolbarButtonClass}
              onClick={() => editor.chain().focus().sinkListItem("listItem").run()}
              disabled={!editor.can().sinkListItem("listItem")}
            >
              ≡›
            </button>

            <ToolbarDivider />

            <button
              type="button"
              title="Link beszúrása"
              aria-label="Link beszúrása"
              className={`${toolbarButtonClass} ${editor.isActive("link") ? activeButtonClass : ""}`}
              onClick={setLink}
            >
              🔗
            </button>
            <button
              type="button"
              title="Link törlése"
              aria-label="Link törlése"
              className={toolbarButtonClass}
              onClick={() => editor.chain().focus().unsetLink().run()}
              disabled={!editor.isActive("link")}
            >
              ⛓×
            </button>
          </div>
        </div>

        <EditorContent editor={editor} />
      </div>
      {error && (
        <p id={`${id}-error`} className="mt-1 text-xs font-normal text-site-danger">
          {error}
        </p>
      )}
    </div>
  );
}
