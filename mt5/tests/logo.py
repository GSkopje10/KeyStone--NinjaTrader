from PIL import Image, ImageDraw, ImageFilter, ImageFont
import math, sys
S=1024; c=S/2
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(len(a)))
img=Image.new("RGBA",(S,S),(0,0,0,0))
# outer glow
glow=Image.new("RGBA",(S,S),(0,0,0,0)); g=ImageDraw.Draw(glow)
g.ellipse([60,60,S-60,S-60],fill=(255,190,60,150)); glow=glow.filter(ImageFilter.GaussianBlur(40)); img.alpha_composite(glow)
# dark disc with radial gradient
disc=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(disc)
for r in range(430,0,-2):
    t=r/430; col=lerp((34,44,70),(8,11,20),t**0.8); d.ellipse([c-r,c-r,c+r,c+r],fill=col+(255,))
img.alpha_composite(disc)
# gold ring: gradient by angle
ring=Image.new("RGBA",(S,S),(0,0,0,0)); rp=ring.load()
gold=[(255,236,170),(255,201,77),(176,120,20),(255,214,110),(255,245,200),(196,140,30),(255,201,77)]
for y in range(S):
    for x in range(S):
        dx=x-c; dy=y-c; rr=math.hypot(dx,dy)
        if 400<=rr<=462 or 372<=rr<=382:
            a=(math.atan2(dy,dx)+math.pi)/(2*math.pi)*(len(gold)-1); i=int(a); t=a-i
            col=lerp(gold[i],gold[min(i+1,len(gold)-1)],t)
            # bevel: lighter at outer edge
            if rr>=400: b=1-abs((rr-431)/31)*0.35; col=tuple(min(255,int(v*b)) for v in col)
            edge=1.0
            for e in (400,462,372,382):
                if abs(rr-e)<1.2: edge=0.5
            rp[x,y]=col+(int(255*edge),)
img.alpha_composite(ring)
# the X: two bevelled diagonal bars, gold over cyan shadow, inside the circle
def bar(draw,ang,w,L,fill):
    a=math.radians(ang); ux,uy=math.cos(a),math.sin(a); px,py=-uy,ux
    pts=[]
    for sgn in (1,-1):
        e=w*0.42; tip=L+w*0.5
        q=[(L,e),(tip,0),(L,-e),(0.18*L,-w*0.62),(-0.18*L,-w*0.62),(-L,-e),(-tip,0),(-L,e),(-0.18*L,w*0.62),(0.18*L,w*0.62)]
        break
    pts=[(c+ux*t+px*o, c+uy*t+py*o) for t,o in q]
    draw.polygon(pts,fill=fill)
sh=Image.new("RGBA",(S,S),(0,0,0,0)); sd=ImageDraw.Draw(sh)
bar(sd,45,92,258,(77,216,255,210)); bar(sd,-45,92,258,(77,216,255,210)); sh=sh.filter(ImageFilter.GaussianBlur(22)); img.alpha_composite(sh)
X=Image.new("RGBA",(S,S),(0,0,0,0)); xd=ImageDraw.Draw(X)
bar(xd,45,78,250,(255,255,255,255)); bar(xd,-45,78,250,(255,255,255,255))
mask=X.split()[3]
grad=Image.new("RGBA",(S,S)); gp=grad.load()
for y in range(S):
    for x in range(S):
        t=(x+y)/(2*S); col=lerp((255,243,196),(255,201,77),min(1,t*1.6)) if t<0.6 else lerp((255,201,77),(170,112,18),(t-0.6)/0.4)
        gp[x,y]=col+(255,)
Xg=Image.new("RGBA",(S,S),(0,0,0,0)); Xg.paste(grad,(0,0),mask); img.alpha_composite(Xg)
# cyan inner stroke on the rising bar for the brand X

# center jewel
j=Image.new("RGBA",(S,S),(0,0,0,0)); jd=ImageDraw.Draw(j); jd.ellipse([c-40,c-40,c+40,c+40],fill=(10,14,24,255)); jd.ellipse([c-26,c-26,c+26,c+26],fill=(77,216,255,255)); jd.ellipse([c-12,c-20,c+4,c-6],fill=(220,250,255,255)); img.alpha_composite(j)
# highlight sheen top
sheen=Image.new("RGBA",(S,S),(0,0,0,0)); shd=ImageDraw.Draw(sheen); shd.ellipse([180,110,S-180,520],fill=(255,255,255,26)); sheen=sheen.filter(ImageFilter.GaussianBlur(30))
m=Image.new("L",(S,S),0); ImageDraw.Draw(m).ellipse([c-400,c-400,c+400,c+400],fill=255); sh2=Image.new("RGBA",(S,S),(0,0,0,0)); sh2.paste(sheen,(0,0),m); img.alpha_composite(sh2)
out=sys.argv[1]
img.resize((512,512),Image.LANCZOS).save(out+"/spasix.png")
img.save(out+"/spasix.ico",sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
img.resize((256,256),Image.LANCZOS).save(out+"/preview.png")
