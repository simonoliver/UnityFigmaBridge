# Change Log:

## 1.0.8

Better support for Linear Color Space (@naoya-maeda). Please note that from this release
all imported textures will have sRGB set to true. The FigmaImage component
now supports sRGB textures in both gamma and linear color spaces/

## 1.0.7

- Enhancement - Added support for "Fit" Image mode (@laura-copop)
- Enhancement - Added support for flipped nodes (@laura-copop)
- Bug fix - JSON Deserialisation no longer throws errors for missing items (@laura-copop)
- Bug fix - Correctly uses project colorspace (@laura-copop)
- Bug fix - Non-visible fills no longer default to visible (@laura-copop)
- Bug fix - Server side images now batch where required to prevent Figma API errors

## 1.0.6

- Enhancement - Added page sync selection (thanks @SatoruUeda)
- Enhancement - Implemented correct alignment for auto-layout
- Enhancement - Use Server-side rendering for boolean shapes
- Bug Fix - Fix for crash import for deeply nested components
- Change - Auto layout components are disabled by default
- Bug fix - Masked objects no longer render the masks themselves

## 1.0.5

Small release adding a few new Figma features and bugs. Thanks @SatoruUeda for the contributions and for all the reports.

- Enhancement -Added support for "Fill" Image mode
- Enhancement -Added text auto sizing
- Enhancement -Added image fill opacity support
- Enhancement -Added layer opacity support
- Bug fix - Fixed issues with components being overwritten
- Bug fix - Numerous other small bugfixes