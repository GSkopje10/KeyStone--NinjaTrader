from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
import math, sys
S=1024; c=S/2
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(len(a)))
img=Image.new("RGBA",(S,S),(0,0,0,0))
glow=Image.new("RGBA",(S,S),(0,0,0,0)); ImageDraw.Draw(glow).ellipse([50,50,S-50,S-50],fill=(255,190,60,140)); img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(38)))
# disc
disc=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(disc)
for r in range(440,0,-2):
    t=r/440; d.ellipse([c-r,c-r,c+r,c+r],fill=lerp((30,40,66),(7,10,18),t**0.8)+(255,))
img.alpha_composite(disc)
# gold ring, angle gradient + bevel
ring=Image.new("RGBA",(S,S),(0,0,0,0)); rp=ring.load()
gold=[(255,236,170),(255,201,77),(170,112,20),(255,214,110),(255,247,205),(190,135,28),(255,236,170)]
for y in range(S):
    for x in range(S):
        dx=x-c; dy=y-c; rr=math.hypot(dx,dy)
        if 408<=rr<=466 or 384<=rr<=392:
            a=(math.atan2(dy,dx)+math.pi)/(2*math.pi)*(len(gold)-1); i=int(a); t=a-i
            col=lerp(gold[i],gold[min(i+1,len(gold)-1)],t)
            if rr>=408: b=1-abs((rr-437)/29)*0.38; col=tuple(min(255,int(v*b)) for v in col)
            edge=0.55 if min(abs(rr-e) for e in (408,466,384,392))<1.2 else 1.0
            rp[x,y]=col+(int(255*edge),)
img.alpha_composite(ring)
# gradient fill helper
def grad_fill(mask, top, mid, bot):
    g=Image.new("RGBA",(S,S)); gp=g.load()
    for y in range(S):
        t=y/S; col=lerp(top,mid,t/0.5) if t<0.5 else lerp(mid,bot,(t-0.5)/0.5)
        for x in range(S): gp[x,y]=col+(255,)
    out=Image.new("RGBA",(S,S),(0,0,0,0)); out.paste(g,(0,0),mask); return out
font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",520)
m=Image.new("L",(S,S),0); md=ImageDraw.Draw(m); bb=md.textbbox((0,0),"S",font=font); w=bb[2]-bb[0]; h=bb[3]-bb[1]
ox=c-w/2-bb[0]-58; oy=c-h/2-bb[1]-6; md.text((ox,oy),"S",font=font,fill=255)
# shadow, dark outline, gold body, top highlight
sh=Image.new("RGBA",(S,S),(0,0,0,0)); sh.paste((0,0,0,200),(14,18),m); img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
outline=m.filter(ImageFilter.MaxFilter(13)); o=Image.new("RGBA",(S,S),(0,0,0,0)); o.paste((60,38,6,255),(0,0),outline); img.alpha_composite(o)
img.alpha_composite(grad_fill(m,(255,244,200),(255,196,60),(176,112,18)))
inner=ImageChops.subtract(m, m.filter(ImageFilter.MinFilter(9)))  # bevel edge
hl=Image.new("RGBA",(S,S),(0,0,0,0)); hl.paste((255,250,225,150),(0,0),inner); img.alpha_composite(hl)
shine=Image.new("L",(S,S),0); ImageDraw.Draw(shine).ellipse([c-420,c-520,c+260,c-40],fill=120); shine=ImageChops.multiply(shine.filter(ImageFilter.GaussianBlur(30)), m)
sl=Image.new("RGBA",(S,S),(0,0,0,0)); sl.paste((255,255,255,255),(0,0),shine.point(lambda v: v//2)); img.alpha_composite(sl)
# the cyan x — inside the ring, bottom right, glowing
fx=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",250)
mx=Image.new("L",(S,S),0); mxd=ImageDraw.Draw(mx); bx=mxd.textbbox((0,0),"x",font=fx); xw=bx[2]-bx[0]; xh=bx[3]-bx[1]
px=c+105-bx[0]; py=c+50-bx[1]; mxd.text((px,py),"x",font=fx,fill=255)
gl=Image.new("RGBA",(S,S),(0,0,0,0)); gl.paste((77,216,255,230),(0,0),mx.filter(ImageFilter.MaxFilter(15))); img.alpha_composite(gl.filter(ImageFilter.GaussianBlur(18)))
ox2=Image.new("RGBA",(S,S),(0,0,0,0)); ox2.paste((6,40,60,255),(0,0),mx.filter(ImageFilter.MaxFilter(9))); img.alpha_composite(ox2)
img.alpha_composite(grad_fill(mx,(225,250,255),(77,216,255),(20,140,200)))
# a small sparkle on the ring (top left)
sp=Image.new("RGBA",(S,S),(0,0,0,0)); spd=ImageDraw.Draw(sp); sx,sy=c-310,c-310
for ang,L in ((0,60),(90,60),(45,26),(135,26)):
    a=math.radians(ang); spd.line([(sx-L*math.cos(a),sy-L*math.sin(a)),(sx+L*math.cos(a),sy+L*math.sin(a))],fill=(255,250,230,255),width=6)
img.alpha_composite(sp.filter(ImageFilter.GaussianBlur(1.5))); spd.ellipse([sx-9,sy-9,sx+9,sy+9],fill=(255,255,255,255)); img.alpha_composite(sp)
out=sys.argv[1]
img.resize((512,512),Image.LANCZOS).save(out+"/spasix.png")
img.save(out+"/spasix.ico",sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
img.resize((256,256),Image.LANCZOS).save(out+"/preview.png")
