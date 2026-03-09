using System.Text;

namespace ConsoleApp1
{
    // Утилітний клас для серіалізації/десеріалізації даних у байтовий потік
    internal class Packet : IDisposable
    {
        // Перелік підтримуваних типів даних (маркер перед значенням)
        public enum DataType : byte
        {
            Int = 1,
            Float = 2,
            Bool = 3,
            String = 4,
            Bytes = 5
        }

        private List<byte> bufferList = new List<byte>();
        private byte[] bufferArray = Array.Empty<byte>();
        private int readPos = 0;
        private bool disposed = false;

        // Конструктор для створення нового порожнього пакету
        public Packet()
        {
            bufferList = new List<byte>();
            readPos = 0;
        }

        // Конструктор, який ініціалізує пакет існуючими байтами
        public Packet(byte[] data)
        {
            bufferList = new List<byte>(data);
            bufferArray = bufferList.ToArray();
            readPos = 0;
        }

        // Повертає поточні байти пакету (для відправки по мережі)
        // Підготувати масив для відправки: перетворює динамічний список на статичний масив
        public byte[] GetBytesArray()
        {
            bufferArray = bufferList.ToArray();
            return bufferArray;
        }

        // Додає необроблений масив байтів до пакету з маркером і довжиною
        public void WriteBytes(byte[] value)
        {
            bufferList.Add((byte)DataType.Bytes);
            WriteInt(value.Length); 
            bufferList.AddRange(value);
        }

        // Запис цілого числа з маркером типу
        public void WriteInt(int value)
        {
            bufferList.Add((byte)DataType.Int);
            bufferList.AddRange(BitConverter.GetBytes(value));
        }

        // Запис числа з плаваючою комою
        public void WriteFloat(float value)
        {
            bufferList.Add((byte)DataType.Float);
            bufferList.AddRange(BitConverter.GetBytes(value));
        }

        // Запис булевого значення
        public void WriteBool(bool value)
        {
            bufferList.Add((byte)DataType.Bool);
            bufferList.AddRange(BitConverter.GetBytes(value));
        }

        // Запис рядка (довжина + UTF8-байти)
        public void WriteString(string value)
        {
            bufferList.Add((byte)DataType.String);
            //  UTF8 щоб підтримувати кирилицю та інші символи
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteInt(bytes.Length);   // length in bytes
            bufferList.AddRange(bytes);
        }

        // Читання заданої кількості байтів безпосередньо
        public byte[] ReadBytes(int length, bool moveReadPos = true)
        {
            if (bufferList.Count > readPos)
            {
                // вичитуємо масив з внутрішнього буфера на основі довжини
                byte[] value = bufferList.GetRange(readPos, length).ToArray();
                if (moveReadPos)
                {
                    readPos += length;
                }
                return value;
            }
            else
            {
                throw new Exception("Could not read the value.");
            }
        }

        // Читає ціле число, перевіряє маркер типу
        public int ReadInt(bool moveReadPos = true)
        {
            if (bufferList.Count > readPos)
            {
                byte type = bufferArray[readPos];
                if (type != (byte)DataType.Int)
                    throw new Exception($"Expected Int but found {type}");
                readPos++;
                int value = BitConverter.ToInt32(bufferArray, readPos);
                if (moveReadPos)
                {
                    readPos += 4;
                }
                return value;
            }
            else
            {
                throw new Exception("Could not read the value.");
            }
        }

        // Читає float зі зміщенням
        public float ReadFloat(bool moveReadPos = true)
        {
            if (bufferList.Count > readPos)
            {
                byte type = bufferArray[readPos];
                if (type != (byte)DataType.Float)
                    throw new Exception($"Expected Float but found {type}");
                readPos++;
                float value = BitConverter.ToSingle(bufferArray, readPos);
                if (moveReadPos)
                {
                    readPos += 4;
                }
                return value;
            }
            else
            {
                throw new Exception("Could not read the value.");
            }
        }

        // Читає логічне значення
        public bool ReadBool(bool moveReadPos = true)
        {
            if (bufferList.Count > readPos)
            {
                byte type = bufferArray[readPos];
                if (type != (byte)DataType.Bool)
                    throw new Exception($"Expected Bool but found {type}");
                readPos++;
                bool value = BitConverter.ToBoolean(bufferArray, readPos);
                if (moveReadPos)
                {
                    readPos += 1;
                }
                return value;
            }
            else
            {
                throw new Exception("Could not read the value.");
            }
        }

        // Читає рядок: спочатку перевіряє маркер, потім довжину і байти
        public string ReadString(bool moveReadPos = true)
        {
            try
            {
                if (bufferList.Count > readPos)
                {
                    byte type = bufferArray[readPos];
                    if (type != (byte)DataType.String)
                        throw new Exception($"Expected String but found {type}");
                    readPos++;
                }
                int length = ReadInt();
                string value = Encoding.UTF8.GetString(bufferArray, readPos, length);
                if (moveReadPos && value.Length > 0)
                {
                    readPos += length;
                }
                return value;
            }
            catch
            {
                throw new Exception("Could not read value of type 'string'!");
            }
        }

        // Повертає тип наступного елемента без зрушення позиції
        // Переглядає байт типу, який стоїть перед значенням; корисно
        // коли треба вирішити, який метод читання викликати
        public DataType PeekDataType()
        {
            if (bufferList.Count > readPos)
            {
                return (DataType)bufferArray[readPos];
            }
            throw new Exception("No data to peek");
        }

        // Зчитує наступне значення, повертає значення та його CLR-тип
        // Універсальний зчитувач: повертає значення як object та декларує його
        // реальний .NET-тип через out-параметр. Це дозволяє обробляти потік
        // гетерогенних даних у верхньому коді.
        public object ReadNext(out Type dataType)
        {
            if (bufferList.Count <= readPos)
                throw new Exception("No data to read");

            DataType dt = PeekDataType();
            switch (dt)
            {
                case DataType.Int:
                    dataType = typeof(int);
                    return ReadInt();
                case DataType.Float:
                    dataType = typeof(float);
                    return ReadFloat();
                case DataType.Bool:
                    dataType = typeof(bool);
                    return ReadBool();
                case DataType.String:
                    dataType = typeof(string);
                    return ReadString();
                case DataType.Bytes:
                    dataType = typeof(byte[]);
                    readPos++;
                    int len = ReadInt();
                    return ReadBytes(len);
                default:
                    throw new Exception($"Unsupported data type: {dt}");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    bufferList.Clear();
                    readPos = 0;
                }
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}