using System;
using System.Collections.Generic;

namespace UnityFigmaBridge.Editor.FigmaApi
{
    /// <summary>
    /// Serialized class representing a bundle image fill data downloaded from Figma server
    /// </summary>
    public class FigmaImageFillData 
    {
        public bool error;
        public int status;
        public FigmaImageFillMetaData meta;
    }
    
    
    /// <summary>
    /// Serialized class for Figma server render data
    /// </summary>
    public class FigmaServerRenderData 
    {
        public string err;
        public Dictionary<string, string> images;
    }

    /// <summary>
    /// Serialized class for image fill fill data
    /// </summary>
    public class FigmaImageFillMetaData
    {
        public Dictionary<string, string> images;
    }
    
    // C# translation of Figma API classes
    // Data here - https://www.figma.com/developers/api#document-props
    
    public enum NodeType {
        DOCUMENT,
        CANVAS,
        FRAME,
        GROUP,
        VECTOR,
        BOOLEAN_OPERATION,
        STAR,
        LINE,
        ELLIPSE,
        REGULAR_POLYGON,
        RECTANGLE,
        TEXT,
        SLICE,
        COMPONENT,
        COMPONENT_SET,
        INSTANCE,
        STICKY,
        SHAPE_WITH_TEXT,
        CONNECTOR,
        SECTION,
        TABLE,
        TABLE_CELL,
        WASHI_TAPE
    }
    
    
    /// <summary>
    /// Figma file (Document)
    /// </summary>
    public class FigmaFile
    {
        public Node document;
        public Dictionary<string,Component> components;
        public int schemaVersion;
        public Dictionary<string,Style> styles;
        public string name;
        public string lastModified;
        public string thumbnailUrl;
        public string version;
        public string owner;
    }

    /// <summary>
    /// Defines data returned via node request (eg request external components)
    /// Defined here - https://www.figma.com/developers/api#get-file-nodes-endpoint
    /// </summary>
    public class FigmaFileNodes
    {
        public string name;
        public string role;
        public string lastModified;
        public string editorType;
        public string thumbnailUrl;
        public string err;
        public Dictionary<string,FigmaFile> nodes;
    }
    
    public class Node
    {
        /// <summary>
        /// A string uniquely identifying this node within the document
        /// </summary>
        public string id;

        /// <summary>
        /// The name given to the node by the user in the tool.
        /// </summary>
        public string name;
        /// <summary>
        /// Whether or not the node is visible on the canvas.
        /// </summary>
        public bool visible=true; // Default to true
        
        /// <summary>
        /// The type of the node, refer to table below for details
        /// </summary>
        public NodeType type;
        
        /// <summary>
        /// Data written by plugins that is visible only to the plugin that wrote it. Requires the `pluginData` to include the ID of the plugin.
        /// </summary>
        public string pluginData;
        
        /// <summary>
        /// Data written by plugins that is visible to all plugins. Requires the `pluginData` parameter to include the string "shared".
        /// </summary>
        public string sharedPluginData;
        
        /// <summary>
        /// An array of nodes that are direct children of this node
        /// </summary>
        public Node[] children; // For DOCUMENT, CANVAS, FRAME
        
        // For CANVAS
        /// <summary>
        /// Background color of the canvas.
        /// </summary>
        public Color backgroundColor;
        /// <summary>
        ///[DEPRECATED] Node ID that corresponds to the start frame for prototypes.
        /// </summary>
        [Obsolete("This is deprecated with the introduction of multiple flows. Please use the flowStartingPoints field")]
        public string prototypeStartNodeID;

        /// <summary>
        /// An array of flow starting points sorted by its position in the prototype settings panel.
        /// </summary>
        public FlowStartingPoint[] flowStartingPoints;

        /// <summary>
        /// An array of export settings representing images to export from the canvas
        /// </summary>
        public ExportSetting[] exportSettings;
        
        // FOR FRAME
        /// <summary>
        /// if true, layer is locked and cannot be edited
        /// </summary>
        public bool locked = false;
        
        /// <summary>
        /// [DEPRECATED] Background of the node. This is deprecated, as backgrounds for frames are now in the fills field.
        /// </summary>
        public Paint[] background;

        /// <summary>
        /// An array of fill paints applied to the node
        /// </summary>
        public Paint[] fills;

        /// <summary>
        /// An array of stroke paints applied to the node
        /// </summary>
        public Paint[] strokes;

        /// <summary>
        /// The weight of strokes on the node
        /// </summary>
        public float strokeWeight;

        
        public enum StrokeAlign
        {
            INSIDE, // stroke drawn inside the shape boundary
            OUTSIDE, //: stroke drawn outside the shape boundary
            CENTER, //: stroke drawn centered along the shape boundary
        }

        /// <summary>
        /// Position of stroke relative to vector outline, as a string enum
        /// </summary>
        public StrokeAlign strokeAlign;

        /// <summary>
        /// Radius of each corner of the frame if a single radius is set for all corners
        /// </summary>
        public float cornerRadius;

        /// <summary>
        /// Array of length 4 of the radius of each corner of the frame, starting in the top left and proceeding clockwise
        /// </summary>
        /// <returns></returns>
        public float[] rectangleCornerRadii;
    
        /// <summary>
        /// How this node blends with nodes behind it in the scene (see blend mode section for more details)
        /// </summary>
        public BlendMode blendMode;

        /// <summary>
        /// Keep height and width constrained to same ratio
        /// </summary>
        public bool preserveRatio=false;

        /// <summary>
        /// Horizontal and vertical layout constraints for node
        /// </summary>
        public LayoutConstraint constraints;

        public enum LayoutAlign
        {
            INHERIT,
            STRETCH,
            MIN,
            CENTER,
            MAX,
        }
        
        /// <summary>
        /// Determines if the layer should stretch along the parent’s counter axis. This property is only provided for direct children of auto-layout frames.
        /// </summary>
        public LayoutAlign layoutAlign;
        
        /// <summary>
        /// Node ID of node to transition to in prototyping
        /// </summary>
        public string transitionNodeID;
        
        /// <summary>
        /// The duration of the prototyping transition on this node (in milliseconds)
        /// </summary>
        public float transitionDuration;

        /// <summary>
        /// The easing curve used in the prototyping transition on this node
        /// </summary>
        public EasingType transitionEasing;

        /// <summary>
        /// Opacity of the node
        /// </summary>
        public float opacity=1;
            
        /// <summary>
        /// Bounding box of the node in absolute space coordinates
        /// </summary>
        public Rectangle absoluteBoundingBox;

        /// <summary>
        /// Width and height of element. This is different from the width and height of the bounding box in that the
        /// absolute bounding box represents the element after scaling and rotation. Only present if geometry=paths
        /// is passed
        /// </summary>
        public Vector size;

        /// <summary>
        /// The top two rows of a matrix that represents the 2D transform of this node relative to its parent. The bottom row of the matrix is implicitly always (0, 0, 1). Use to transform coordinates in geometry. Only present if geometry=paths is passed
        /// </summary>
        public float[,] relativeTransform;
        // TO DO - Implement

        /// <summary>
        /// Whether or not this node clip content outside of its bounds
        /// </summary>
        public bool clipsContent;

        public enum LayoutMode
        {
            NONE,
            HORIZONTAL,
            VERTICAL,
        }

        /// <summary>
        /// Whether this layer uses auto-layout to position its children
        /// </summary>
        public LayoutMode layoutMode=LayoutMode.NONE;

        public enum PrimaryAxisSizingMode
        {
            FIXED,
            AUTO
        }
        /// <summary>
        /// Whether the primary axis has a fixed length (determined by the user) or an automatic length (determined
        /// by the layout engine). This property is only applicable for auto-layout
        /// </summary>
        public PrimaryAxisSizingMode primaryAxisSizingMode = PrimaryAxisSizingMode.AUTO;

        public enum CounterAxisSizingMode
        {
            FIXED,
            AUTO
        }
        /// <summary>
        /// Whether the counter axis has a fixed length (determined by the user) or an automatic length (determined
        /// by the layout engine). This property is only applicable for auto-layout
        /// </summary>
        public CounterAxisSizingMode counterAxisSizingMode;

        public enum PrimaryAxisAlignItems
        {
            MIN,
            CENTER,
            MAX,
            SPACE_BETWEEN,
        }

        /// <summary>
        /// Determines how the auto-layout frame’s children should be aligned in the primary axis direction. This property is only applicable for auto-layout frames
        /// </summary>
        public PrimaryAxisAlignItems primaryAxisAlignItems=PrimaryAxisAlignItems.MIN;

        public enum CounterAxisAlignItems
        {
            MIN,
            CENTER,
            MAX,
        }
        
        /// <summary>
        /// Determines how the auto-layout frame’s children should be aligned in the counter axis direction. This
        /// property is only applicable for auto-layout frames.
        /// </summary>
        public CounterAxisAlignItems counterAxisAlignItems=CounterAxisAlignItems.MIN;

        /// <summary>
        /// The padding betweeen the left border of the frame and its children. This property is only applicable for auto-layout frames
        /// </summary>
        public float paddingLeft = 0;

        /// <summary>
        /// The padding betweeen the right border of the frame and its children. This property is only applicable for auto-layout frames.
        /// </summary>
        public float paddingRight = 0;

        /// <summary>
        /// The padding betweeen the top border of the frame and its children. This property is only applicable for auto-layout frames
        /// </summary>
        public float paddingTop = 0;

        /// <summary>
        /// The padding betweeen the bottom border of the frame and its children. This property is only applicable for auto-layout frames.
        /// </summary>
        public float paddingBottom = 0;

        /// <summary>
        /// The horizontal padding between the borders of the frame and its children. This property is only applicable for auto-layout frames.
        /// Deprecated in favor of setting individual paddings.
        /// </summary>
        public float horizontalPadding = 0;

        /// <summary>
        /// The vertical padding between the borders of the frame and its children. This property is only applicable for auto-layout frames.
        /// Deprecated in favor of setting individual paddings.
        /// </summary>
        public float verticalPadding = 0;
        
        /// <summary>
        /// The distance between children of the frame. This property is only applicable for auto-layout frames.
        /// </summary>
        public float itemSpacing = 0;
        
        /// <summary>
        /// An array of layout grids attached to this node (see layout grids section for more details).
        /// GROUP nodes do not have this attribute
        /// </summary>
        public LayoutGrid[] layoutGrids;

        public enum OverflowDirection
        {
            NONE,
            HORIZONTAL_SCROLLING,
            VERTICAL_SCROLLING,
            HORIZONTAL_AND_VERTICAL_SCROLLING,
        }

        /// <summary>
        /// Defines the scrolling behavior of the frame, if there exist contents outside of the frame boundaries.
        /// The frame can either scroll vertically, horizontally, or in both directions to the extents of the content
        /// contained within it. This behavior can be observed in a prototype.
        /// </summary>
        public OverflowDirection overflowDirection=OverflowDirection.NONE;

        /// <summary>
        /// An array of effects attached to this node (see effects section for more details)
        /// </summary>
        public Effect[] effects;

        /// <summary>
        /// Does this node mask sibling nodes in front of it?
        /// </summary>
        public bool isMask;

        /// <summary>
        /// Does this mask ignore fill style (like gradients) and effects?
        /// </summary>
        public bool isMaskOutline;


        
        // FOR VECTOR
        
        // TODO - ADD VECTOR PROPERTIES
        
        
        // FOR TEXT
        /// <summary>
        /// Text contained within text box
        /// </summary>
        public string characters;

        /// <summary>
        /// Style of text including font family and weight (see type style section for more information)
        /// </summary>
        public TypeStyle style;

        /// <summary>
        /// Array with same number of elements as characeters in text box, each element is a reference to the
        /// styleOverrideTable defined below and maps to the corresponding character in the characters field.
        /// Elements with value 0 have the default type style
        /// </summary>
        public int[] characterStyleOverrides;

        // TODO- Overrides
        // styleOverrideTableMap<Number,TypeStyle>
         //   Map from ID to TypeStyle for looking up style overrides 

         
        // FOR INSTANCE
        /// <summary>
        /// ID of component that this instance came from, refers to components table (see endpoints section below)
        /// </summary>
        public string componentId;
        
        // FOR ELLIPSE
        public ArcData arcData;
        
    }
    
    public class Color
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    /// <summary>
    /// Format and size to export an asset at
    /// </summary>
    public class ExportSetting
    {
        public string suffix;
        public string format;
        public Constraint Constraint;
    }

    /// <summary>
    /// Sizing constraint for exports
    /// </summary>
    public class Constraint
    {
        public enum ConstraintType
        {
            SCALE,
            WIDTH,
            HEIGHT
        }

        public ConstraintType type;
        public float value;
    }

    /// <summary>
    /// A rectangle that expresses a bounding box in absolute coordinates
    /// </summary>
    public class Rectangle
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }



    /// <summary>
    /// Information about the arc properties of an ellipse. 0° is the x axis and increasing angles rotate clockwise
    /// </summary>
    public class ArcData
    {
        /// <summary>
        /// Start of the sweep in radians   
        /// </summary>
        public float startingAngle;
        /// <summary>
        /// End of the sweep in radians
        /// </summary>
        public float endingAngle;
        /// <summary>
        /// Inner radius value between 0 and 1
        /// </summary>
        public float innerRadius;
    }

    /// <summary>
    /// Enum describing how layer blends with layers below
    /// </summary>
    public enum BlendMode
    {
        // Normal blends:
        PASS_THROUGH, // (only applicable to objects with children)
        NORMAL,

        //Darken:
        DARKEN,
        MULTIPLY,
        LINEAR_BURN,
        COLOR_BURN,

        //Lighten:
        LIGHTEN,
        SCREEN,
        LINEAR_DODGE,
        COLOR_DODGE,

        //Contrast:
        OVERLAY,
        SOFT_LIGHT,
        HARD_LIGHT,

        //Inversion:
        DIFFERENCE,
        EXCLUSION,

        //Component:
        HUE,
        SATURATION,
        COLOR,
        LUMINOSITY,
    }

    /// <summary>
    /// Enum describing animation easing curves
    /// </summary>
    public enum EasingType
    {
        EASE_IN,// Ease in with an animation curve similar to CSS ease-in.
        EASE_OUT,   // Ease out with an animation curve similar to CSS ease-out.
        EASE_IN_AND_OUT, // Ease in and then out with an animation curve similar to CSS ease-in-out.
        LINEAR,          // No easing, similar to CSS linear.
        GENTLE_SPRING, // Gentle spring animation similar to react-spring.
        CUSTOM_BEZIER, 
        QUICK,
        GENTLE,
        BOUNCY,
        SLOW,
        CUSTOM_SPRING,
        EASE_IN_BACK,
        EASE_OUT_BACK,
        EASE_IN_AND_OUT_BACK
    }

    /// <summary>
    /// Layout constraint relative to containing Frame
    /// </summary>
    public class LayoutConstraint
    {
        public enum VerticalLayoutConstraint
        {
            TOP, // Node is laid out relative to top of the containing frame
            BOTTOM, // Node is laid out relative to bottom of the containing frame
            CENTER, // Node is vertically centered relative to containing frame
            TOP_BOTTOM, //Both top and bottom of node are constrained relative to containing frame (node stretches with frame)
            SCALE, // Node scales vertically with containing frame
        }

        public enum HorizontalLayoutConstraint
        {
            LEFT, // Node is laid out relative to left of the containing frame
            RIGHT, // Node is laid out relative to right of the containing frame
            CENTER, // Node is horizontally centered relative to containing frame
            LEFT_RIGHT, //  Both left and right of node are constrained relative to containing frame (node stretches with frame)
            SCALE, //: Node scales horizontally with containing frame
        }

        public VerticalLayoutConstraint vertical;
        public HorizontalLayoutConstraint horizontal;
    }

    /// <summary>
    /// Guides to align and place objects within a frame
    /// </summary>
    public class LayoutGrid
    {
        public enum Pattern
        {
            COLUMNS,
            ROWS,
            GRID,
        }
        /// <summary>
        /// Orientation of the grid as a string enum
        /// </summary>
        public Pattern pattern;
        
        /// <summary>
        /// Width of column grid or height of row grid or square grid spacing
        /// </summary>
        public float sectionSize;

        /// <summary>
        /// Is the grid currently visible?
        /// </summary>
        public bool visible;

        /// <summary>
        /// Color of the grid
        /// </summary>
        public Color color;
        
        // The following properties are only meaningful for directional grids (COLUMNS or ROWS)

        public enum Alignment
        {
            MIN, // Grid starts at the left or top of the frame
            STRETCH, // Grid is stretched to fit the frame
            CENTER, // Grid is center aligned
            MAX, // Grid is right or bottom
        }

        /// <summary>
        /// Positioning of grid as a string enum
        /// </summary>
        public Alignment alignment;

        /// <summary>
        /// Spacing in between columns and rows
        /// </summary>
        public float gutterSize;

        /// <summary>
        /// Spacing before the first column or row
        /// </summary>
        public float offset;
        
        /// <summary>
        /// Number of columns or rows
        /// </summary>
        public float count;

    }
    
    
    /// <summary>
    /// A visual effect such as a shadow or blur
    /// </summary>
    public class Effect
    {
        public enum EffectType
        {
            INNER_SHADOW,
            DROP_SHADOW,
            LAYER_BLUR,
            BACKGROUND_BLUR,
        }
        /// <summary>
        /// Type of effect as a string enum
        /// </summary>
        public EffectType type;
        
        /// <summary>
        /// Is the effect active?
        /// </summary>
        public bool visible;

        /// <summary>
        /// Radius of the blur effect (applies to shadows as well)
        /// </summary>
        public float radius;

        // The following properties are for shadows only:
        
        /// <summary>
        /// The color of the shadow
        /// </summary>
        public Color color;

        /// <summary>
        /// Blend mode of the shadow
        /// </summary>
        public BlendMode blendMode;

        /// <summary>
        /// How far the shadow is projected in the x and y directions
        /// </summary>
        public Vector offset;

        /// <summary>
        /// How far the shadow spreads
        /// </summary>
        public float spread=0;

    }

    /// <summary>
    /// A link to either a URL or another frame (node) in the document
    /// </summary>
    public class Hyperlink
    {
        public enum HyperlinkType
        {
            URL,
            NODE,
        }
        
        /// <summary>
        /// Type of hyperlink
        /// </summary>
        public HyperlinkType type;
        
        /// <summary>
        /// URL being linked to, if URL type
        /// </summary>
        public string url;

        /// <summary>
        /// ID of frame hyperlink points to, if NODE type
        /// </summary>
        public string nodeID;
    }

    /// <summary>
    /// A solid color, gradient, or image texture that can be applied as fills or strokes
    /// </summary>
    public class Paint
    {
        public enum PaintType
        {
            SOLID,
            GRADIENT_LINEAR,
            GRADIENT_RADIAL,
            GRADIENT_ANGULAR,
            GRADIENT_DIAMOND,
            IMAGE,
            EMOJI,
        }

        /// <summary>
        /// Type of paint as a string enum
        /// </summary>
        public PaintType type;
        
        /// <summary>
        /// Is the paint enabled?
        /// </summary>
        public bool visible=true;

        /// <summary>
        /// Overall opacity of paint (colors within the paint can also have opacity values which would blend with this)
        /// </summary>
        public float opacity=1;

        //For solid paints:
        
        /// <summary>
        /// Solid color of the paint
        /// </summary>
        public Color color;
        
        
        // For gradient paints:
        
        /// <summary>
        /// How this node blends with nodes behind it in the scene
        /// </summary>
        public BlendMode blendMode;
        
        /// <summary>
        /// This field contains three vectors, each of which are a position in normalized object space (normalized object space is if the top left corner of the bounding box of the object is (0, 0) and the bottom right is (1,1)). The first position corresponds to the start of the gradient (value 0 for the purposes of calculating gradient stops), the second position is the end of the gradient (value 1), and the third handle position determines the width of the gradient. See image examples below:
        /// </summary>
        public Vector[] gradientHandlePositions;

        /// <summary>
        /// Positions of key points along the gradient axis with the colors anchored there. Colors along the gradient are interpolated smoothly between neighboring gradient stops
        /// </summary>
        public ColorStop[] gradientStops;

        // For Image paints

        public enum ScaleMode
        {
            FILL,
            FIT,
            TILE,
            STRETCH,
        }
        
        /// <summary>
        /// Image scaling mode
        /// </summary>
        public ScaleMode scaleMode;

        /// <summary>
        /// Affine transform applied to the image, only present if scaleMode is STRETCH
        /// </summary>
        /// <returns></returns>
        public float[,] imageTransform;  // This is 2D float array

        /// <summary>
        /// Amount image is scaled by in tiling, only present if scaleMode is TILE
        /// </summary>
        public float scalingFactor;

        /// <summary>
        /// Image rotation, in degrees.
        /// </summary>
        public float rotation;

        /// <summary>
        /// A reference to an image embedded in this node. To download the image using this reference, use the GET file images endpoint to retrieve the mapping from image references to image URLs
        /// </summary>
        public string imageRef;
        
        /// <summary>
        /// A reference to the GIF embedded in this node, if the image is a GIF. To download the image using this reference, use the GET file images endpoint to retrieve the mapping from image references to image URLs
        /// </summary>
        public string gifRef;

    }
    /// <summary>
    /// A 2d vector
    /// </summary>
    public class Vector
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// A width and a height
    /// </summary>
    public class Size
    {
        public float width;
        public float height;
    }

    /*
    // TODO - Fix this up. Prob needs to be setup as array of array higher up     
    /// <summary>
    /// A 2x3 affine transformation matrix
    /// </summary>    
    public class Transform
    {
        
        //A 2D affine transformation matrix that can be used to calculate the affine transforms applied to a layer, including scaling, rotation, shearing, and translation.
        //The form of the matrix is given as an array of 2 arrays of 3 numbers each. E.g. the identity matrix would be [[1, 0, 0], [0, 1, 0]].
    }
    */

    /// <summary>
    /// A relative offset within a frame
    /// </summary>
    public class FrameOffset
    {
        public string node_id; // Unique id specifying the frame.
        public Vector node_offset; // 2d vector offset within the frame.
    }

    /// <summary>
    /// A position color pair representing a gradient stop
    /// </summary>
    public class ColorStop
    {
        public float position; // Value between 0 and 1 representing position along gradient axis
        public Color color; //Color attached to corresponding position
    }

    /// <summary>
    /// Metadata for character formatting
    /// </summary>
    public class TypeStyle
    {
        /// <summary>
        /// Font family of text (standard name)
        /// </summary>
        public string fontFamily;
        
        /// <summary>
        /// PostScript font name
        /// </summary>
        public string fontPostScriptName;

        /// <summary>
        /// Space between paragraphs in px, 0 if not present
        /// </summary>
        public float paragraphSpacingNumber;

        /// <summary>
        /// Paragraph indentation in px, 0 if not present
        /// </summary>
        public float paragraphIndentNumber;

        /// <summary>
        /// Whether or not text is italicized
        /// </summary>
        public bool italic;

        /// <summary>
        /// Numeric font weight
        /// </summary>
        public int fontWeight;

        /// <summary>
        /// Font size in px
        /// </summary>
        public float fontSize;

        public enum TextCase
        {
            ORIGINAL,
            UPPER,
            LOWER,
            TITLE,
            SMALL_CAPS,
            SMALL_CAPS_FORCED
        }
        
        /// <summary>
        /// Text casing applied to the node, default is the original casing
        /// </summary>
        public TextCase textCase=TextCase.ORIGINAL;

        public enum TextDecoration
        {
            NONE,
            STRIKETHROUGH,
            UNDERLINE
        }

        /// <summary>
        /// Text decoration applied to the node, default is none
        /// </summary>
        public TextDecoration textDecoration = TextDecoration.NONE;

        public enum TextAutoResize
        {
            NONE,
            HEIGHT,
            WIDTH_AND_HEIGHT,
            TRUNCATE
        }

        /// <summary>
        /// Dimensions along which text will auto resize, default is that the text does not auto-resize.
        /// </summary>
        public TextAutoResize textAutoResize = TextAutoResize.NONE;

        public enum TextAlignHorizontal
        {
            LEFT,
            RIGHT,
            CENTER,
            JUSTIFIED,
        }
        
        /// <summary>
        /// Horizontal text alignment as string enum
        /// </summary>
        public TextAlignHorizontal textAlignHorizontal;
        
        public enum TextAlignVertical
        {
            TOP,
            CENTER,
            BOTTOM,
        }

        /// <summary>
        /// Vertical text alignment as string enum
        /// </summary>
        public TextAlignVertical textAlignVertical;

        /// <summary>
        /// Space between characters in px
        /// </summary>
        public float letterSpacing;

        /// <summary>
        /// Paints applied to characters
        /// </summary>
        public Paint[] fills;

        public Hyperlink hyperlink;
        
        /*
         * opentypeFlagsMap<String, Number> default: {}
A map of OpenType feature flags to 1 or 0, 1 if it is enabled and 0 if it is disabled. Note that some flags aren't reflected here. For example, SMCP (small caps) is still represented by the textCase fiel
         */

        /// <summary>
        /// Line height in px
        /// </summary>
        public float lineHeightPx;

        /// <summary>
        /// Line height as a percentage of normal line height. This is deprecated; in a future version of the API only lineHeightPx and lineHeightPercentFontSize will be returned
        /// </summary>
        public float lineHeightPercent=100;

        /// <summary>
        /// Line height as a percentage of the font size. Only returned when lineHeightPercent is not 100.
        /// </summary>
        public float lineHeightPercentFontSize;
        
        /// <summary>
        /// The unit of the line height value specified by the user.
        /// Can be
        /// PIXELS
        /// FONT_SIZE_%
        /// INTRINSIC_%
        /// </summary>
        public string lineHeightUnitString;
    }

    /// <summary>
    /// A description of a main component. Helps you identify which component instances are attached to
    /// </summary>
    public class Component
    {
        /// <summary>
        /// The key of the component
        /// </summary>
        public string key;

        /// <summary>
        /// The name of the component
        /// </summary>
        public string name;

        /// <summary>
        /// The description of the component as entered in the editor
        /// </summary>
        public string description;
    }

    /// <summary>
    /// A set of properties that can be applied to nodes and published.
    /// Styles for a property can be created in the corresponding property's panel while editing a file
    /// </summary>
    public class Style
    {
        /// <summary>
        /// The key of the style
        /// </summary>
        public string key;

        /// <summary>
        /// The name of the style
        /// </summary>
        public string name;
        /// <summary>
        /// The description of the style
        /// </summary>
        public string description;

        public enum StyleType
        {
            FILL,
            TEXT,
            EFFECT,
            GRID,
        }

        public StyleType style_type;
    }


    /// <summary>
    /// A flow starting point used when launching a prototype to enter Presentation view
    /// </summary>
    public class FlowStartingPoint
    {
        /// <summary>
        /// Unique identifier specifying the frame
        /// </summary>
        public string nodeId;
        
        /// <summary>
        /// Name of flow
        /// </summary>
        public string name;
    }
   
    
    
}
