--[[
Hks has 4 main non-equality comparison ops: LT, LT_BK, LE, LE_BK.
LT and LE are "less than" and "less than or equal" respectively. B is the left side of the comparison and is always a
register. C is the right side and can either be a register or a constant. When A is 1, the comparison is inverted:
LT effectively becomes "greater than or equal" and LE becomes "greater than". This means that all comparisons can be
implemented in two ops. LT_BK and LE_BK are similar to LT and LE with the key difference being that B is always a constant
instead of a register and C is a register.

When selecting instructions for a comparison expression the compiler seems to go through the following steps:
1: If either the left or the right are complex expressions they get evaluated and their results put into temp registers
2: If the comparison is '>' or '>=' then the operands are swapped and the comparison is replaced with '<' and '<='
   respectively.
3: If the left side is a constant after the flipping, then either a LT_BK or LE_BK is emitted. Otherwise LT or LE are
   emitted.
4: Once emitted the instruction opcode and operands will never change, but A can be flipped if needed such as when
   implementing an "or" conditional or "not" a condition.
--]]

-- Constants:
-- 0: 4
-- 1: 6
-- 2: 2
-- 3: f
-- 4: nil
-- 5: nil
-- 6: 0
-- 7: 90
-- 8: 99

-- 0000: LOADK 0 0           -- R(0) := 4
-- 0001: LOADK 1 1           -- R(1) := 6
local a = 4
local b = 6

-- Simple compare
--[[
0002: LE_BK 0 2 0         
0003: JMP 0 65538         
0004: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0005: DATA 20 4           
0006: CALL_I_R1 2 1 1   
--]]
if a >= 2 then
    f()
end

--[[
0007: LT_BK 0 2 0         
0008: JMP 0 65538         
0009: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0010: DATA 20 4           
0011: CALL_I_R1 2 1 1   
--]]
if a > 2 then
    f()
end

--[[
0012: LT 0 0 -2           
0013: JMP 0 65538         
0014: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0015: DATA 20 4           
0016: CALL_I_R1 2 1 1   
--]]
if a < 2 then
    f()
end

--[[
0017: LE 0 0 -2           
0018: JMP 0 65538         
0019: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0020: DATA 20 4           
0021: CALL_I_R1 2 1 1 
--]]
if a <= 2 then
    f()
end 

-- Compound left
--[[
0022: LT_BK 1 6 0         
0023: JMP 0 65537         
0024: LT_BK 0 6 1         
0025: JMP 0 65538         
0026: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0027: DATA 20 4           
0028: CALL_I_R1 2 1 1 
--]]
if (a > 0 or b > 0) then
    f()
end

--[[
0029: LT_BK 1 6 0         
0030: JMP 0 65537         
0031: LE_BK 0 6 1         
0032: JMP 0 65538         
0033: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0034: DATA 20 4           
0035: CALL_I_R1 2 1 1
--]]
if (a > 0 or b >= 0) then
    f()
end

--[[
0036: LT_BK 1 6 0         
0037: JMP 0 65537         
0038: LT 0 1 -6           
0039: JMP 0 65538         
0040: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0041: DATA 20 4           
0042: CALL_I_R1 2 1 1 
--]]
if (a > 0 or b < 0) then
    f()
end

--[[
0043: LT_BK 1 6 0         
0044: JMP 0 65537         
0045: LE 0 1 -6           
0046: JMP 0 65538         
0047: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0048: DATA 20 4           
0049: CALL_I_R1 2 1 1  
--]]
if (a > 0 or b <= 0) then
    f()
end

--[[
0050: LE_BK 1 6 0         
0051: JMP 0 65537         
0052: LT_BK 0 6 1         
0053: JMP 0 65538         
0054: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0055: DATA 20 4           
0056: CALL_I_R1 2 1 1   
--]]
if (a >= 0 or b > 0) then
    f()
end

--[[
0057: LE_BK 1 6 0         
0058: JMP 0 65537         
0059: LE_BK 0 6 1         
0060: JMP 0 65538         
0061: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0062: DATA 20 4           
0063: CALL_I_R1 2 1 1    
--]]
if (a >= 0 or b >= 0) then
    f()
end

--[[
0064: LE_BK 1 6 0         
0065: JMP 0 65537         
0066: LT 0 1 -6           
0067: JMP 0 65538         
0068: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0069: DATA 20 4           
0070: CALL_I_R1 2 1 1  
--]]
if (a >= 0 or b < 0) then
    f()
end

--[[
0071: LE_BK 1 6 0         
0072: JMP 0 65537         
0073: LE 0 1 -6           
0074: JMP 0 65538         
0075: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0076: DATA 20 4           
0077: CALL_I_R1 2 1 1   
--]]
if (a >= 0 or b <= 0) then
    f()
end

--[[
0078: LT 1 0 -6           
0079: JMP 0 65537         
0080: LT_BK 0 6 1         
0081: JMP 0 65538         
0082: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0083: DATA 20 4           
0084: CALL_I_R1 2 1 1   
--]]
if (a < 0 or b > 0) then
    f()
end

--[[
0085: LT 1 0 -6           
0086: JMP 0 65537         
0087: LE_BK 0 6 1         
0088: JMP 0 65538         
0089: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0090: DATA 20 4           
0091: CALL_I_R1 2 1 1  
--]]
if (a < 0 or b >= 0) then
    f()
end

--[[
0092: LT 1 0 -6           
0093: JMP 0 65537         
0094: LT 0 1 -6           
0095: JMP 0 65538         
0096: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0097: DATA 20 4           
0098: CALL_I_R1 2 1 1
--]]
if (a < 0 or b < 0) then
    f()
end

--[[
0099: LT 1 0 -6           
0100: JMP 0 65537         
0101: LE 0 1 -6           
0102: JMP 0 65538         
0103: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0104: DATA 20 4           
0105: CALL_I_R1 2 1 1     
--]]
if (a < 0 or b <= 0) then
    f()
end

--[[
0106: LE_BK 1 6 0         
0107: JMP 0 65537         
0108: LE 0 1 -6           
0109: JMP 0 65538         
0110: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0111: DATA 20 4           
0112: CALL_I_R1 2 1 1  
--]]
if (a >= 0 or b <= 0) then
    f()
end

--[[
0113: LE_BK 1 6 0         
0114: JMP 0 65537         
0115: LT_BK 0 6 1         
0116: JMP 0 65538         
0117: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0118: DATA 20 4           
0119: CALL_I_R1 2 1 1    
--]]
if (a >= 0 or b > 0) then
    f()
end

--[[
0120: LE_BK 1 6 0         
0121: JMP 0 65537         
0122: LE_BK 0 6 1         
0123: JMP 0 65538         
0124: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0125: DATA 20 4           
0126: CALL_I_R1 2 1 1   
--]]
if (a >= 0 or b >= 0) then
    f()
end

--[[
0127: LE_BK 1 6 0         
0128: JMP 0 65537         
0129: LT 0 1 -6           
0130: JMP 0 65538         
0131: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0132: DATA 20 4           
0133: CALL_I_R1 2 1 1  
--]]
if (a >= 0 or b < 0) then
    f()
end

--[[
0134: LE_BK 1 6 0         
0135: JMP 0 65537         
0136: LE 0 1 -6           
0137: JMP 0 65538         
0138: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0139: DATA 20 4           
0140: CALL_I_R1 2 1 1    
--]]
if (a >= 0 or b <= 0) then
    f()
end

-- Compound right
--[[
0141: LT 1 0 -6           
0142: JMP 0 65537         
0143: LT 0 1 -6           
0144: JMP 0 65538         
0145: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0146: DATA 20 4           
0147: CALL_I_R1 2 1 1 
--]]
if (0 > a or 0 > b) then
    f()
end

--[[
0148: LT 1 0 -6           
0149: JMP 0 65537         
0150: LE 0 1 -6           
0151: JMP 0 65538         
0152: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0153: DATA 20 4           
0154: CALL_I_R1 2 1 1   
--]]
if (0 > a or 0 >= b) then
    f()
end

--[[
0155: LT 1 0 -6           
0156: JMP 0 65537         
0157: LT_BK 0 6 1         
0158: JMP 0 65538         
0159: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0160: DATA 20 4           
0161: CALL_I_R1 2 1 1  
--]]
if (0 > a or 0 < b) then
    f()
end

--[[
0162: LT 1 0 -6           
0163: JMP 0 65537         
0164: LE_BK 0 6 1         
0165: JMP 0 65538         
0166: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0167: DATA 20 4           
0168: CALL_I_R1 2 1 1   
--]]
if (0 > a or 0 <= b) then
    f()
end

--[[
0169: LE 1 0 -6           
0170: JMP 0 65537         
0171: LT 0 1 -6           
0172: JMP 0 65538         
0173: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0174: DATA 20 4           
0175: CALL_I_R1 2 1 1   
--]]
if (0 >= a or 0 > b) then
    f()
end

--[[
0176: LE 1 0 -6           
0177: JMP 0 65537         
0178: LE 0 1 -6           
0179: JMP 0 65538         
0180: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0181: DATA 20 4           
0182: CALL_I_R1 2 1 1     
--]]
if (0 >= a or 0 >= b) then
    f()
end

--[[
0183: LE 1 0 -6           
0184: JMP 0 65537         
0185: LT_BK 0 6 1         
0186: JMP 0 65538         
0187: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0188: DATA 20 4           
0189: CALL_I_R1 2 1 1    
--]]
if (0 >= a or 0 < b) then
    f()
end

--[[
0190: LE 1 0 -6           
0191: JMP 0 65537         
0192: LE_BK 0 6 1         
0193: JMP 0 65538         
0194: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0195: DATA 20 4           
0196: CALL_I_R1 2 1 1   
--]]
if (0 >= a or 0 <= b) then
    f()
end

--[[
0197: LT_BK 1 6 0         
0198: JMP 0 65537         
0199: LT 0 1 -6           
0200: JMP 0 65538         
0201: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0202: DATA 20 4           
0203: CALL_I_R1 2 1 1     
--]]
if (0 < a or 0 > b) then
    f()
end

--[[
0204: LT_BK 1 6 0         
0205: JMP 0 65537         
0206: LE 0 1 -6           
0207: JMP 0 65538         
0208: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0209: DATA 20 4           
0210: CALL_I_R1 2 1 1    
--]]
if (0 < a or 0 >= b) then
    f()
end

--[[
0211: LT_BK 1 6 0         
0212: JMP 0 65537         
0213: LT_BK 0 6 1         
0214: JMP 0 65538         
0215: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0216: DATA 20 4           
0217: CALL_I_R1 2 1 1  
--]]
if (0 < a or 0 < b) then
    f()
end

--[[
0218: LT_BK 1 6 0         
0219: JMP 0 65537         
0220: LE_BK 0 6 1         
0221: JMP 0 65538         
0222: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0223: DATA 20 4           
0224: CALL_I_R1 2 1 1   
--]]
if (0 < a or 0 <= b) then
    f()
end

--[[
0225: LE_BK 1 6 0         
0226: JMP 0 65537         
0227: LT 0 1 -6           
0228: JMP 0 65538         
0229: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0230: DATA 20 4           
0231: CALL_I_R1 2 1 1   
--]]
if (0 <= a or 0 > b) then
    f()
end

--[[
0232: LE_BK 1 6 0         
0233: JMP 0 65537         
0234: LE 0 1 -6           
0235: JMP 0 65538         
0236: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0237: DATA 20 4           
0238: CALL_I_R1 2 1 1   
--]]
if (0 <= a or 0 >= b) then
    f()
end

--[[
0239: LE_BK 1 6 0         
0240: JMP 0 65537         
0241: LT_BK 0 6 1         
0242: JMP 0 65538         
0243: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0244: DATA 20 4           
0245: CALL_I_R1 2 1 1   
--]]
if (0 <= a or 0 < b) then
    f()
end

--[[
0246: LE_BK 1 6 0         
0247: JMP 0 65537         
0248: LE_BK 0 6 1         
0249: JMP 0 65538         
0250: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0251: DATA 20 4           
0252: CALL_I_R1 2 1 1   
--]]
if (0 <= a or 0 <= b) then
    f()
end

--[[
0253: LE_BK 0 7 0         
0254: JMP 0 65540         
0255: LT 0 0 -8           
0256: JMP 0 65538         
0257: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0258: DATA 20 4           
0259: CALL_I_R1 2 1 1    
--]]
if a >= 90 and a < 99 then
    f()
end

--[[
0260: LE_BK 0 7 0         
0261: JMP 0 65540         
0262: LT 0 0 -8           
0263: JMP 0 65538         
0264: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0265: DATA 20 4           
0266: CALL_I_R1 2 1 1    
--]]
if 90 <= a and 99 > a then
    f()
end

--[[
0267: LE_BK 0 7 0         
0268: JMP 0 65540         
0269: LT 0 0 -8           
0270: JMP 0 65538         
0271: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0272: DATA 20 4           
0273: CALL_I_R1 2 1 1   
--]]
if 90 <= a and a < 99 then
    f()
end

--[[
0274: LT 1 0 -7           
0275: JMP 0 65537         
0276: LE_BK 0 8 0         
0277: JMP 0 65538         
0278: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0279: DATA 20 4           
0280: CALL_I_R1 2 1 1    
--]]
if 90 > a or a >= 99 then
    f()
end

--[[
0281: LT 1 0 -7           
0282: JMP 0 65540         
0283: LE_BK 1 8 0         
0284: JMP 0 65538         
0285: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0286: DATA 20 4           
0287: CALL_I_R1 2 1 1     
0288: RETURN 0 1 0        -- return 
--]]
if not (90 > a or a >= 99) then
    f()
end

--[[
0288: LT 0 0 -7           
0289: JMP 0 65540
0290: LE_BK 0 8 0         
0291: JMP 0 65538         
0292: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0293: DATA 20 4           
0294: CALL_I_R1 2 1 1 
]]--
if 90 > a and a >= 99 then
    f()
end

--[[
0288: LT 0 0 -7           
0289: JMP 0 65537         
0290: LE_BK 1 8 0         
0291: JMP 0 65538         
0292: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0293: DATA 20 4           
0294: CALL_I_R1 2 1 1  
]]--
if not (90 > a and a >= 99) then
    f()
end

--[[
0302: LT 0 0 -7           
0303: JMP 0 65542         
0304: LE_BK 0 8 0         
0305: JMP 0 65540         
0306: LT_BK 0 9 1         
0307: JMP 0 65538         
0308: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0309: DATA 20 4           
0310: CALL_I_R1 2 1 1 
]]--
if 90 > a and a >= 99 and b > 50 then
    f()
end

--[[
0311: LT 0 0 -7           
0312: JMP 0 65539         
0313: LE_BK 0 8 0         
0314: JMP 0 65537         
0315: LT_BK 1 9 1         
0316: JMP 0 65538         
0317: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0318: DATA 20 4           
0319: CALL_I_R1 2 1 1  
]]--
if not (90 > a and a >= 99 and b > 50) then
    f()
end

--[[
0288: LT 0 1 0            
0289: JMP 0 65538         
0290: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0291: DATA 20 4           
0292: CALL_I_R1 2 1 1  
--]]
if a > b then
    f()
end

--[[
0293: LE 0 1 0            
0294: JMP 0 65538         
0295: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0296: DATA 20 4           
0297: CALL_I_R1 2 1 1   
--]]
if a >= b then
    f()
end

--[[
0298: LT 0 0 1            
0299: JMP 0 65538         
0300: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0301: DATA 20 4           
0302: CALL_I_R1 2 1 1  
--]]
if a < b then
    f()
end

--[[
0303: LE 0 0 1            
0304: JMP 0 65538         
0305: GETGLOBAL_MEM 2 3   -- R(2) := Gbl[f]
0306: DATA 20 4           
0307: CALL_I_R1 2 1 1 
--]]
if a <= b then
    f()
end