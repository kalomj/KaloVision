using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaloVision
{
    public class CircularBuffer
    {
        Queue<double> _queue;
        int _size;

        public CircularBuffer(int size)
        {
            _queue = new Queue<double>(size);
            _size = size;
        }

        public void Add(double obj)
        {
            if (_queue.Count == _size)
            {
                _queue.Dequeue();
                _queue.Enqueue(obj);
            }
            else
                _queue.Enqueue(obj);
        }
        public double Read()
        {
            return _queue.Dequeue();
        }

        public double Peek()
        {
            return _queue.Peek();
        }

        public double Avg()
        {
            return _queue.Average();
        }

        public double Avg(int size)
        {
            return _queue.Reverse().Take(size).Average();
        }

        public double Median(int size)
        {
            if (_queue.Count-1 < (size / 2))
            {
                return 0.0;
            }

            return _queue.Reverse().Take(size).OrderBy(n => n).ElementAt(size / 2);
        }

        public int Count()
        {
            return _queue.Count;
        }
    }
}
