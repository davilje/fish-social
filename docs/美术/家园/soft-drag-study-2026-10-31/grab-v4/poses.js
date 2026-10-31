// Coordinates are in each isolated orange sprite, NOT in an auto-fitted canvas.
// Fur variants share the source atlas registration. Paws have persistent identities.
export const grips = {head:'头部',body:'身体中部',frontLeft:'前爪①',frontRight:'前爪②',hindLeft:'后爪①',hindRight:'后爪②'};
const p=(file,size,headWidth,head,body,fl,fr,hl,hr,tail,extra={})=>({file,size,headWidth,head,body,frontLeft:fl,frontRight:fr,hindLeft:hl,hindRight:hr,tail,...extra});
export const poses={
 sit:p('rest-0',[582,607],430,[335,205],[330,400],[324,562],[419,562],[210,542],[525,535],[130,470],{headGrab:[330,130]}),
 crouch:p('rest-1',[695,500],413,[218,190],[470,300],[115,446],[260,458],[475,441],[565,450],[610,220],{headGrab:[215,130]}),
 supine:p('rest-2',[790,469],390,[205,190],[460,330],[330,301],[446,192],[580,308],[620,195],[716,310],{headGrab:[200,130]}),
 middle0:p('lift-0',[440,345],260,[127,120],[293,144],[96,308],[191,306],[323,282],[365,282],[388,218]),
 middle1:p('lift-1',[439,369],260,[129,129],[284,89],[103,332],[213,320],[319,285],[347,260],[390,240]),
 middle2:p('lift-2',[426,391],260,[141,187],[284,96],[161,346],[220,304],[320,280],[290,245],[380,325]),
 front0:p('lift-3',[387,480],306,[182,154],[202,322],[199,426],[348,267],[140,438],[288,437],[72,395]),
 front1:p('lift-4',[368,540],287,[167,186],[232,349],[246,416],[320,41],[184,502],[292,493],[73,423]),
 front2:p('lift-5',[376,589],300,[178,203],[248,381],[251,449],[335,44],[187,552],[288,518],[81,457]),
 leftFront:p('paws-3',[440,489],265,[284,210],[216,309],[175,42],[262,399],[144,399],[208,451],[53,277]),
 leftHind0:p('paws-1',[511,441],280,[375,250],[258,175],[278,400],[367,405],[123,40],[182,341],[47,170]),
 rightHind0:p('paws-2',[469,444],280,[154,251],[276,180],[129,400],[240,406],[346,340],[328,40],[423,180]),
 leftHind:p('paws-4',[444,499],270,[296,342],[257,210],[177,391],[251,457],[177,38],[124,279],[52,116]),
 rightHind:p('paws-5',[445,480],270,[135,344],[233,201],[270,379],[217,442],[319,260],[329,36],[410,171]),
 headHang:p('head-atlas',[519,789],414,[307,202],[303,466],[194,421],[421,433],[240,730],[381,730],[94,622],{headGrab:[306,117]}),
};
export const sequences={head:['headHang'],body:['middle0','middle1','middle2'],frontLeft:['leftFront'],frontRight:['front0','front1','front2'],hindLeft:['leftHind0','leftHind'],hindRight:['rightHind0','rightHind']};
export const anchor=(pose,mode)=>mode==='head'?(pose.headGrab||pose.head):pose[mode];
