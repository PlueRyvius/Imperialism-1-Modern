# Run with Godot --headless --path src/Imperialism.Client --script <absolute script> -- <absolute PNG>.
# CPU font atlas rendering at final (2x) sizes; no game screenshots or 1x enlargement.
extends SceneTree

var page = Color("f0e4c5")
var ink = Color("5e4a30")
var image = Image.create(1200, 720, false, Image.FORMAT_RGBA8)
var ts = TextServerManager.get_primary_interface()

func face(path):
    var f = FontFile.new()
    f.data = FileAccess.get_file_as_bytes("res://art/fonts/" + path)
    f.antialiasing = TextServer.FONT_ANTIALIASING_GRAY
    f.hinting = TextServer.HINTING_NORMAL
    f.oversampling = 1
    return f

func draw_text(font, text, size, origin):
    var shaped = ts.create_shaped_text()
    ts.shaped_text_add_string(shaped, text, font.get_rids(), size)
    ts.shaped_text_shape(shaped)
    var pen = origin
    var blended = 0
    for glyph in ts.shaped_text_get_glyphs(shaped):
        var rid = glyph.font_rid
        var index = glyph.index
        ts.font_render_glyph(rid, Vector2i(size, 0), index)
        var uv = ts.font_get_glyph_uv_rect(rid, Vector2i(size, 0), index)
        var offset = ts.font_get_glyph_offset(rid, Vector2i(size, 0), index)
        var texture = ts.font_get_glyph_texture_idx(rid, Vector2i(size, 0), index)
        if texture >= 0:
            var atlas = ts.font_get_texture_image(rid, Vector2i(size, 0), texture)
            for repeat in glyph.repeat:
                for y in int(uv.size.y):
                    for x in int(uv.size.x):
                        var alpha = atlas.get_pixel(int(uv.position.x)+x, int(uv.position.y)+y).a
                        var p = Vector2i(pen + glyph.offset + offset) + Vector2i(x,y)
                        if alpha > 0 and p.x >= 0 and p.x < image.get_width() and p.y >= 0 and p.y < image.get_height():
                            image.set_pixelv(p, image.get_pixelv(p).lerp(ink, alpha))
                            if alpha < 1: blended += 1
                pen.x += glyph.advance
        else:
            pen.x += glyph.advance * glyph.repeat
    ts.free_rid(shaped)
    return blended

func _initialize():
    image.fill(page)
    var original_body = face("antiqua.ttf")
    var original_heading = face("display_bold.ttf")
    var body = face("transport-lora.ttf")
    var heading = FontVariation.new()
    heading.base_font = face("transport-fraunces.ttf")
    heading.variation_opentype = {"wght":700, "opsz":34, "SOFT":50}
    var samples = ["Transport", "Ledger", "Fish & Livestock", "15 / 19", "119 / 120", "Spring, 1882", "$10,000", "Give Transport Orders"]
    var aa = 0
    for col in 2:
        var x = 36 + col * 600
        draw_text(body, "Original bundled faces" if col == 0 else "Lora / Fraunces Bold (OFL)", 24, Vector2(x,40))
        for i in samples.size():
            var font = (original_heading if col == 0 else heading) if i < 2 else (original_body if col == 0 else body)
            var size = 32 if i < 2 else (16 if i < 4 else (18 if i == 4 else 20))
            aa += draw_text(font, samples[i], size, Vector2(x, 110 + i * 75))
    var output = OS.get_cmdline_user_args()[0]
    var result = image.save_png(output)
    print("FONT_SAMPLE size=1200x720 finalScale=2 fractionalAlphaPixels=", aa, " save=", result)
    print("BODY longestNameAt8=", body.get_string_size("Fish & Livestock", HORIZONTAL_ALIGNMENT_LEFT, -1, 8), " figureAt8=",body.get_string_size("15 / 19",HORIZONTAL_ALIGNMENT_LEFT,-1,8))
    print("TITLES ", heading.get_string_size("Transport",HORIZONTAL_ALIGNMENT_LEFT,-1,16)," / ",heading.get_string_size("Ledger",HORIZONTAL_ALIGNMENT_LEFT,-1,16))
    quit(0 if result == OK else 1)
