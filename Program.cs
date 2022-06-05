using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Chip_8
{
    class Program
    {

        static void Main(string[] args)
        {
            CPU cpu = new CPU();

            using (BinaryReader reader = new BinaryReader(new FileStream("C:/Users/Hanzo/Desktop/Programming/C#/Chip-8/TestRoms/IBM Logo.ch8", FileMode.Open)))
            {
                foreach(ushort opcode in ExtractChip8RomOpcodes(reader))
                {
                    //Console.WriteLine($"{opcode.ToString("X4")}");
                    try
                    {
                        cpu.ExecuteOpcode(opcode);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                
            }

            Console.ReadKey();
        }


        static IEnumerable<ushort> ExtractChip8RomOpcodes(BinaryReader reader)
        {
            int numberOfOpcodes = (int)((reader.BaseStream.Length / 2));
            ushort[] result = new ushort[numberOfOpcodes];
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                //bits are shifted 8 to left, and ored with the original to turn into big endian
                var opcode = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
                result[(reader.BaseStream.Position / 2) - 1] = opcode;
            }
            return result;
        }
    }




    public class CPU
    {
        public byte[] RAM = new byte[4096];  // 4K of memory
        public byte[] V = new byte[16]; // V0-VF
        public ushort PC = 0; // the instruction pointer
        public ushort I = 0; // general purpose memory register
        public Stack<ushort> Stack = new Stack<ushort>(); //ToDo: Limit the size of this stack to 24 (maybe just make an inherited class)

        public byte DelayTimer;
        public byte SoundTimer;

        public byte Keyboard;

        //ToDo: make this a bitmask, or make it grayscale
        public byte[] Display = new byte[64 * 32]; // 0 is on, anything else is off. (for now)
        Random rng = new Random(Environment.TickCount);

        public void ExecuteOpcode(ushort opcode)
        {
            ushort firstNibble = (ushort)(opcode & 0xF000);

            switch(firstNibble)   //TODO: abstract to an external state machine
            {
                case 0x0000:
                    if(opcode == 0x00e0) //Clear Display
                    {
                        for(int i = 0; i < Display.Length; i++) Display[i] = 0;
                    }
                    else if(opcode == 0x00ee) // Return from subroutine
                    {
                        PC = Stack.Pop();
                    }
                    else
                    {
                        throw new Exception($"Unsupported Opcode: {opcode.ToString("X4")}");
                    }
                    break;

                case 0x1000: //Jump to 0x0NNN
                    PC = (ushort)(opcode & 0x0FFF);  // gets the last three nibbles
                    break;

                case 0x2000: //Call Subroutine at 0x0NNN
                    Stack.Push(PC); //save your place
                    PC = (ushort)(opcode & 0x0FFF);
                    break;

                case 0x3000: //skips next instruction if VX and 0x00NN are equal
                    if (V[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF)) PC += 2;
                    break;

                case 0x4000: // skips next instruction if VX and 0x00NN are not equal
                    if (V[(opcode & 0x0F00) >> 8] != (opcode & 0x00FF)) PC += 2; /// shifting so the value is index value is in the ones place
                    break;

                case 0x5000: // skips next instruction if VX and VY are equal
                    if ((V[(opcode & 0x0F00) >> 8] == V[(opcode & 0x00F0) >> 4]) & (((opcode & 0x000F) == 0))) PC += 2;  //Added check that last digit is zero
                    break;

                case 0x6000: // Sets VX to 0x00NN
                    V[(opcode & 0x0F00) >> 8] = (byte)(opcode & 0x00FF);
                    break;

                case 0x7000: // adds 0x00NN to VX
                    V[(opcode & 0x0F00) >> 8] += (byte)(opcode & 0x00FF);
                    break;

                case 0x8000: // bitwise operations with VX and VY (0x8XYN) where N indicates the operation
                    int vxIndex = (opcode & 0x0F00) >> 8;
                    int vyIndex = (opcode & 0x00F0) >> 4;
                    switch (opcode & 0x000F)
                    {
                        case 0: V[vxIndex] = V[vyIndex]; break; //assignment
                        case 1: V[vxIndex] = (byte)(V[vxIndex] | V[vyIndex]); break; //OR
                        case 2: V[vxIndex] = (byte)(V[vxIndex] & V[vyIndex]); break; //AND
                        case 3: V[vxIndex] = (byte)(V[vxIndex] ^ V[vyIndex]); break; //XOR
                        case 4: //ADD (with carry)
                            V[15] = (byte)(V[vxIndex] + V[vyIndex] > 255 ? 1 : 0); // set the carry bit to true if sum is out of bounds
                            V[vxIndex] = (byte)((V[vxIndex] + V[vyIndex]) & 0x00FF); // remember that these registers only hold 8 bits
                            break;
                        
                        case 5: //SUB (with borrow)
                            V[15] = (byte)(V[vxIndex] > V[vyIndex] ? 1 : 0); // set the carry bit to true if sum is out of bounds
                            V[vxIndex] = (byte)((V[vxIndex] - V[vyIndex]) & 0x00FF); // remember that these registers only hold 8 bits
                            break;
                        
                        case 6: // store the least significant bit from VX and put it in V15, shift right
                            V[15] = (byte)(V[vxIndex] & 0x0001); //store the least significant bit in VF
                            V[vxIndex] = (byte)(V[vxIndex] >> 1);
                            break;

                        case 7: // set flag if VY is greater than VX and then subtract VY from VX
                            V[15] = (byte)(V[vyIndex] > V[vxIndex] ? 1: 0);
                            V[vxIndex] = (byte)((V[vxIndex] - V[vyIndex]) & 0x00FF); 
                            break;

                        case 0xE: //store the most significant bit from VX into V15 and shift left
                            V[15] = (byte)(((V[vxIndex] & 0x80) == 0x80) ? 1 : 0);
                            V[vxIndex] = (byte)(V[vxIndex] << 1);
                            break;
                        default:
                            throw new Exception($"Unsupported Opcode: {opcode.ToString("X4")}");
                    }
                    break;

                case 0x9000: //Skip next instruction if VX and VY !=
                    if (V[(opcode & 0x0F00) >> 8] != V[(opcode & 0x00FF) >> 4]) PC += 2;
                    break;

                case 0xA000: // Set I to 0x0NNN
                    I = (ushort)(opcode & 0x0FFF);
                    break;

                case 0xB000: // jump to 0x0NNN + V0
                    PC = (ushort)((opcode & 0x0FFF) + V[0]);
                    break;

                case 0xC000: // generate random number and & it with 0x00FF, then put in VX
                    V[(opcode & 0x0F00) >> 8] = (byte)(rng.Next() & (opcode & 0x00FF));
                    break;

                case 0xD000: //display n bytes of sprite data from RAM starting at memory location I
                    int x = V[(opcode & 0x0F00) >> 8];
                    int y = V[(opcode & 0x00F0) >> 4];
                    int n = opcode & 0x000F;
                    V[15] = 0;
                    
                    for(int i=0; i < n; i++) //iterate through each byte specified by n ?each new byte is a vertical line?
                    {
                        byte mem = RAM[I];
                        for (int j=0; j<8; j++) //iterate through each bit in that byte (horizontal)
                        {
                            byte pixel = (byte)((mem >> (7 - j)) & 0x01); //contains only one bit of the byte because 7 puts the most significant bit in the least significant place. then decrement by j for each place
                            int displayIndex = (x + j) + (y + i) * 64; 
                            if (pixel == 1 && Display[displayIndex] == 1) V[15] = 1; // if there is a pixel collision set collision to true
                            Display[displayIndex] = (byte)(Display[displayIndex] ^ pixel); // each pixel is XORed with it's current value
                        }
                    }
                    break;
                case 0xE000:
                    if((opcode & 0x00FF) == 0x009E) //skip next instruction if Keyboard input == X
                    {
                        if(((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) == 0x01) PC += 2; // I really need to parse this sucker out but...  the first shift hurts my brain
                        break;
                    }
                    else if ((opcode & 0x00FF) == 0x00A1)
                    {
                        if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) != 0x01) PC += 2;
                        break;
                    }
                    else throw new Exception($"Unsupported Opcode: {opcode.ToString("X4")}");
                case 0xF000:
                    switch (opcode & 0x00FF)
                    {
                        case 0x0007:
                            //stuff
                            break;

                        case 0x000A:
                            //stuff
                            break;

                        case 0x0015:
                            //stuff
                            break;

                        case 0x0018:
                            //stuff
                            break;

                        case 0x001E:
                            //stuff
                            break;

                        case 0x0029:
                            //stuff
                            break;

                        case 0x0033:
                            //stuff
                            break;

                        case 0x0055:
                            //stuff
                            break;

                        case 0x0065:
                            //stuff
                            break;

                        default:
                            throw new Exception($"Unsupported Opcode: {opcode.ToString("X4")}");
                    }
                    break;
                default:
                    throw new Exception($"Unsupported Opcode: {opcode.ToString("X4")}"); 
                    
            }
        }
    }
}
