/* NOTICE: This code contains a protected, proprietary implementation of a Universal CRC generator.
   DO NOT REDISTRIBUTE - NO PERMISSION IS GIVEN FROM THE AUTHOR TO REDISTRIBUTE, MAKE PUBLICLY AVAILABLE, OR TO SHOW TO ANY OTHER ENTITY.

   "Ryan McDonald" is the only recipient that is permitted to possess/view this file.

   If you have received this file in error, please delete/destroy immediately.

   Copyright 2023 Kenneth Vorseth
*/
namespace CRC_PROPRIETARY {
    public enum DEGREE { CRC4, CRC5, CRC7, CRC8, CRC15, CRC16, CRC24, CRC32 };

    public struct CRC {
        public DEGREE deg;
        public string name;
        public UInt32 poly;
        public UInt32 init;
        public   bool ref_in;
        public   bool ref_out;
        public UInt32 xor_out;
        public UInt32 result;

        public CRC(DEGREE _deg, string _name, UInt32 _poly, UInt32 _init, bool _ref_in, bool _ref_out, UInt32 _xor_out) {
            deg = _deg; name = _name; poly = _poly; init = _init; ref_in = _ref_in; ref_out = _ref_out; xor_out = _xor_out; result = _init;
        }  
    }

    class BIT_VECTOR {
        public UInt32 vector;
        public BIT_VECTOR() { vector = 0;}
        public BIT_VECTOR(UInt32 _vector) { vector = _vector; }
        public void Set(UInt32 val) { vector = val; }
        public UInt32 Get() { return vector; }
        public bool Get(int idx) {
            return ((vector & (1 << idx)) != 0);
        }
        public void Set(int idx, bool val) {
            if (val) { vector |= (UInt32)(1 << idx);  }
            else     { vector &= (UInt32)~(1 << idx); }
        }

        //[#] slice operator
        public bool this[int idx]
        {
            get => Get(idx);
            set => Set(idx, (bool)value);
        }
    }

    class LFSR_CRC {
        private static BIT_VECTOR? POLY_NOMIAL;
        private static         int POLY_DEGREE;

        public LFSR_CRC() { POLY_NOMIAL = new BIT_VECTOR(); }

        //Serial LFSR Update Formula (Galois LFSR with optional data-in)
        private static UInt32 LFSR_Serial(UInt32 _prev, bool din=false) {
            if (POLY_NOMIAL == null) { return 0;}
            BIT_VECTOR prev = new BIT_VECTOR(_prev);
            BIT_VECTOR poly_en = new BIT_VECTOR();
            BIT_VECTOR next = new BIT_VECTOR();

            for (int i=0; i <= POLY_DEGREE-1; i++) {
                //AND Gates (Poly enables to feed XORs)
                poly_en[i] = POLY_NOMIAL[i] & prev[POLY_DEGREE-1];
                //XOR Gates
                if (i==0) { next[i] = din ^ poly_en[i];       }
                else      { next[i] = prev[i-1] ^ poly_en[i]; }
            }
            return next.Get(); //return next LFSR value
        }

        //Parallel LFSR Update Formula
        private static UInt32 LFSR_Parallel(int n, byte _din, UInt32 init) {
            BIT_VECTOR x = new BIT_VECTOR(init);
            BIT_VECTOR din  = new BIT_VECTOR(_din);
            //Run LFSR (N) Cycles
            if (n > 0) {
                for (int i=1; i <= n; i++) {
                    if (i < 8) { x.Set(LFSR_Serial(x.Get(), din[i])); }
                    else       { x.Set(LFSR_Serial(x.Get(), false));  }
                }
            }
            return x.Get();
        }

        //CRC Computation (modifies CRC.result)
        public static void ComputeCRC(ref CRC _crc, byte[] _data) {
            POLY_NOMIAL = new BIT_VECTOR(_crc.poly);
            switch (_crc.deg) {
                case DEGREE.CRC4: POLY_DEGREE = 4; break;
                case DEGREE.CRC5: POLY_DEGREE = 5; break;
                case DEGREE.CRC7: POLY_DEGREE = 7; break;
                case DEGREE.CRC8: POLY_DEGREE = 8; break;
                case DEGREE.CRC15: POLY_DEGREE = 15; break;
                case DEGREE.CRC16: POLY_DEGREE = 16; break;
                case DEGREE.CRC24: POLY_DEGREE = 24; break;
                case DEGREE.CRC32: POLY_DEGREE = 32; break;
            }

            //Compute H1-Matrix for LFSR
            BIT_VECTOR[] matrix_h1 = new BIT_VECTOR[8];
            for (int h=0; h < matrix_h1.Length; h++) { matrix_h1[h] = new BIT_VECTOR(); }
            for (int n=0; n <= 7; n++) { matrix_h1[n].Set(LFSR_Parallel(n, 0, POLY_NOMIAL.Get() )); }
            //Compute H2-Matrix for LFSR
            BIT_VECTOR[] matrix_h2 = new BIT_VECTOR[POLY_DEGREE];
            for (int h=0; h < matrix_h2.Length; h++) { matrix_h2[h] = new BIT_VECTOR(); }
            for (int n=0; n <= POLY_DEGREE-1; n++) { matrix_h2[n].Set(LFSR_Parallel(8, 0, (UInt32)(1 << n) )); }

            //Initialize LFSR
            BIT_VECTOR gen = new BIT_VECTOR(_crc.init);
            //Console.WriteLine(String.Format("LFSR: {0:X8}", gen.Get()));
            //Console.WriteLine(String.Format("POLY: {0:X8}", POLY_NOMIAL.Get()));

            //Iterate Over Data
            foreach (byte b in _data) {
                //REF_IN (reverse bits in each byte)
                BIT_VECTOR dat_in  = new BIT_VECTOR(b);
                BIT_VECTOR dat_rev = new BIT_VECTOR(b);
                if (_crc.ref_in) {
                    for (int d=0; d < 8; d++) { dat_rev[d] = dat_in[7-d]; }
                }
                
                //CRC Computation
                BIT_VECTOR var_h1_column = new BIT_VECTOR();
                BIT_VECTOR var_h2_column = new BIT_VECTOR();
                BIT_VECTOR var_u = new BIT_VECTOR();
                //Foreach column (i) in H1
                for (int i=0; i <= POLY_DEGREE-1; i++) {
                    //Populate H1 column vector
                    var_h1_column.Set(0);
                    for (int c=0; c <= 7; c++) {
                        var_h1_column[c] = matrix_h1[c][i];
                    }

                    //Foreach bit (j) in column vector
                    for (int j=0; j <= 7; j++) {
                        //Append input data term if bit (j) set
                        if (var_h1_column[j]) {
                            var_u[i] = var_u[i] ^ dat_rev[j];
                        }
                    }
                }

                //Foreach column (i) in H2
                for (int i=0; i <= POLY_DEGREE-1; i++) {
                    //Populate H2 column vector
                    var_h2_column.Set(0);
                    for (int c=0; c <= POLY_DEGREE-1; c++) {
                        var_h2_column[c] = matrix_h2[c][i];
                    }

                    //Foreach bit (j) in column vector
                    for (int j=0; j <= POLY_DEGREE-1; j++) {
                        //Append state term if bit (j) set
                        if (var_h2_column[j]) {
                            var_u[i] = var_u[i] ^ gen[j];
                        }
                    }
                }
                gen.Set(var_u.Get());
            }
            //Console.WriteLine(String.Format(" GEN: {0:X8}", gen.Get()));

            //REF_OUT (reverse all bits)
            BIT_VECTOR gen_rev = new BIT_VECTOR(gen.Get());
            if (_crc.ref_out) {
                for (int d=0; d < POLY_DEGREE; d++) { gen_rev[d] = gen[POLY_DEGREE-d-1]; }
            }

            //XOR_OUT
            _crc.result = gen_rev.Get() ^ _crc.xor_out;
        }
    }
}